﻿/*
    Copyright (C) 2011 de4dot@gmail.com

    This file is part of de4dot.

    de4dot is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    de4dot is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with de4dot.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.MyStuff;
using de4dot.blocks;

namespace de4dot.deobfuscators.dotNET_Reactor {
	class MethodsDecrypter {
		ModuleDefinition module;
		EncryptedResource encryptedResource;

		public bool Detected {
			get { return encryptedResource.Method != null; }
		}

		public TypeDefinition DecrypterType {
			get { return encryptedResource.Type; }
		}

		public MethodDefinition Method {
			get { return encryptedResource.Method; }
		}

		public EmbeddedResource Resource {
			get { return encryptedResource.Resource; }
		}

		public MethodsDecrypter(ModuleDefinition module) {
			this.module = module;
			this.encryptedResource = new EncryptedResource(module);
		}

		public MethodsDecrypter(ModuleDefinition module, MethodsDecrypter oldOne) {
			this.module = module;
			this.encryptedResource = new EncryptedResource(module, oldOne.encryptedResource);
		}

		public void find() {
			var additionalTypes = new string[] {
//				"System.Diagnostics.StackFrame",	//TODO: Not in DNR <= 3.7.0.3
				"System.IntPtr",
//				"System.Reflection.Assembly",		//TODO: Not in unknown DNR version with jitter support
			};
			var checkedMethods = new Dictionary<MethodReferenceAndDeclaringTypeKey, bool>();
			var callCounter = new CallCounter();
			int typesLeft = 30;
			foreach (var type in module.GetTypes()) {
				var cctor = DotNetUtils.getMethod(type, ".cctor");
				if (cctor == null || cctor.Body == null)
					continue;
				if (typesLeft-- <= 0)
					break;

				foreach (var info in DotNetUtils.getCalledMethods(module, cctor)) {
					var method = info.Item2;
					var key = new MethodReferenceAndDeclaringTypeKey(method);
					if (!checkedMethods.ContainsKey(key)) {
						checkedMethods[key] = false;
						if (info.Item1.BaseType == null || info.Item1.BaseType.FullName != "System.Object")
							continue;
						if (!DotNetUtils.isMethod(method, "System.Void", "()"))
							continue;
						if (!encryptedResource.couldBeResourceDecrypter(method, additionalTypes))
							continue;
						checkedMethods[key] = true;
					}
					else if (!checkedMethods[key])
						continue;
					callCounter.add(method);
				}
			}

			encryptedResource.Method = (MethodDefinition)callCounter.most();
		}

		static short[] nativeLdci4 = new short[] { 0x55, 0x8B, 0xEC, 0xB8, -1, -1, -1, -1, 0x5D, 0xC3 };
		static short[] nativeLdci4_0 = new short[] { 0x55, 0x8B, 0xEC, 0x33, 0xC0, 0x5D, 0xC3 };
		public bool decrypt(PE.PeImage peImage, ISimpleDeobfuscator simpleDeobfuscator, ref Dictionary<uint, DumpedMethod> dumpedMethods) {
			if (encryptedResource.Method == null)
				return false;

			encryptedResource.init(simpleDeobfuscator);
			if (!encryptedResource.FoundResource)
				return false;
			var methodsData = encryptedResource.decrypt();

			bool hooksJitter = findDnrCompileMethod(encryptedResource.Method.DeclaringType) != null;

			long xorKey = getXorKey();
			if (xorKey != 0) {
				// DNR 4.3, 4.4
				var stream = new MemoryStream(methodsData);
				var reader = new BinaryReader(stream);
				var writer = new BinaryWriter(stream);
				int count = methodsData.Length / 8;
				for (int i = 0; i < count; i++) {
					long val = reader.ReadInt64();
					val ^= xorKey;
					stream.Position -= 8;
					writer.Write(val);
				}
			}

			var methodsDataReader = new BinaryReader(new MemoryStream(methodsData));
			int patchCount = methodsDataReader.ReadInt32();
			int mode = methodsDataReader.ReadInt32();

			int tmp = methodsDataReader.ReadInt32();
			methodsDataReader.BaseStream.Position -= 4;
			if ((tmp & 0xFF000000) == 0x06000000) {
				// It's method token + rva. DNR 3.7.0.3 (and earlier?) - 3.9.0.1
				methodsDataReader.BaseStream.Position += 8L * patchCount;
				patchCount = methodsDataReader.ReadInt32();
				mode = methodsDataReader.ReadInt32();

				patchDwords(peImage, methodsDataReader, patchCount);
				while (methodsDataReader.BaseStream.Position < methodsData.Length - 1) {
					uint token = methodsDataReader.ReadUInt32();
					int numDwords = methodsDataReader.ReadInt32();
					patchDwords(peImage, methodsDataReader, numDwords / 2);
				}
			}
			else if (!hooksJitter || mode == 1) {
				// DNR 3.9.8.0, 4.0, 4.1, 4.2, 4.3, 4.4
				patchDwords(peImage, methodsDataReader, patchCount);
				while (methodsDataReader.BaseStream.Position < methodsData.Length - 1) {
					uint rva = methodsDataReader.ReadUInt32();
					uint token = methodsDataReader.ReadUInt32();	// token, unknown, or index
					int size = methodsDataReader.ReadInt32();
					if (size > 0)
						peImage.dotNetSafeWrite(rva, methodsDataReader.ReadBytes(size));
				}
			}
			else {
				// DNR 4.0 - 4.4 (jitter is hooked)

				var metadataTables = peImage.Cor20Header.createMetadataTables();
				var methodDef = metadataTables.getMetadataType(PE.MetadataIndex.iMethodDef);
				var rvaToIndex = new Dictionary<uint, int>((int)methodDef.rows);
				uint offset = methodDef.fileOffset;
				for (int i = 0; i < methodDef.rows; i++) {
					uint rva = peImage.offsetReadUInt32(offset);
					offset += methodDef.totalSize;
					if (rva == 0)
						continue;

					if ((peImage.readByte(rva) & 3) == 2)
						rva++;
					else
						rva += (uint)(4 * (peImage.readByte(rva + 1) >> 4));
					rvaToIndex[rva] = i;
				}

				patchDwords(peImage, methodsDataReader, patchCount);
				int count = methodsDataReader.ReadInt32();
				dumpedMethods = new Dictionary<uint, DumpedMethod>();
				bool foundNativeCode = false;
				while (methodsDataReader.BaseStream.Position < methodsData.Length - 1) {
					uint rva = methodsDataReader.ReadUInt32();
					uint index = methodsDataReader.ReadUInt32();
					bool isNativeCode = index >= 0x70000000;
					int size = methodsDataReader.ReadInt32();
					var methodData = methodsDataReader.ReadBytes(size);

					int methodIndex;
					if (!rvaToIndex.TryGetValue(rva, out methodIndex)) {
						Log.w("Could not find method having code RVA {0:X8}", rva);
						continue;
					}

					if (isNativeCode) {
						if (!foundNativeCode) {
							foundNativeCode = true;
							Log.w("Found native code. Assembly won't run.");
						}
						//TODO: Convert to CIL code
						Log.v("Found native code. Ignoring it for now... Assembly won't run. token: {0:X8}", 0x06000001 + methodIndex);

						// Convert return true / false methods. The others are converted to
						// throw 0xDEADCODE.
						if (isCode(nativeLdci4, methodData)) {
							uint val = BitConverter.ToUInt32(methodData, 4);
							methodData = new byte[] { 0x20, 0, 0, 0, 0, 0x2A };
							methodData[1] = (byte)val;
							methodData[2] = (byte)(val >> 8);
							methodData[3] = (byte)(val >> 16);
							methodData[4] = (byte)(val >> 24);
						}
						else if (isCode(nativeLdci4_0, methodData)) {
							methodData = new byte[] { 0x16, 0x2A };
						}
						else
							methodData = new byte[] { 0x20, 0xDE, 0xC0, 0xAD, 0xDE, 0x7A };
					}

					var dm = new DumpedMethod();
					dm.token = (uint)(0x06000001 + methodIndex);
					dm.code = methodData;

					offset = methodDef.fileOffset + (uint)(methodIndex * methodDef.totalSize);
					rva = peImage.offsetReadUInt32(offset);
					dm.mdImplFlags = peImage.offsetReadUInt16(offset + (uint)methodDef.fields[1].offset);
					dm.mdFlags = peImage.offsetReadUInt16(offset + (uint)methodDef.fields[2].offset);
					dm.mdName = peImage.offsetRead(offset + (uint)methodDef.fields[3].offset, methodDef.fields[3].size);
					dm.mdSignature = peImage.offsetRead(offset + (uint)methodDef.fields[4].offset, methodDef.fields[4].size);
					dm.mdParamList = peImage.offsetRead(offset + (uint)methodDef.fields[5].offset, methodDef.fields[5].size);

					if ((peImage.readByte(rva) & 3) == 2) {
						dm.mhFlags = 2;
						dm.mhMaxStack = 8;
						dm.mhCodeSize = (uint)dm.code.Length;
						dm.mhLocalVarSigTok = 0;
					}
					else {
						dm.mhFlags = peImage.readUInt16(rva);
						dm.mhMaxStack = peImage.readUInt16(rva + 2);
						dm.mhCodeSize = (uint)dm.code.Length;
						dm.mhLocalVarSigTok = peImage.readUInt32(rva + 8);
					}

					dumpedMethods[dm.token] = dm;
				}
			}

			return true;
		}

		static bool isCode(short[] nativeCode, byte[] code) {
			if (nativeCode.Length != code.Length)
				return false;
			for (int i = 0; i < nativeCode.Length; i++) {
				if (nativeCode[i] == -1)
					continue;
				if ((byte)nativeCode[i] != code[i])
					return false;
			}
			return true;
		}

		static void patchDwords(PE.PeImage peImage, BinaryReader reader, int count) {
			for (int i = 0; i < count; i++) {
				uint rva = reader.ReadUInt32();
				uint data = reader.ReadUInt32();
				peImage.dotNetSafeWrite(rva, BitConverter.GetBytes(data));
			}
		}

		long getXorKey() {
			var instructions = encryptedResource.Method.Body.Instructions;
			for (int i = 0; i < instructions.Count - 1; i++) {
				if (instructions[i].OpCode.Code != Code.Ldind_I8)
					continue;
				var ldci4 = instructions[i + 1];
				if (!DotNetUtils.isLdcI4(ldci4))
					continue;

				return DotNetUtils.getLdcI4Value(ldci4);
			}
			return 0;
		}

		public static MethodDefinition findDnrCompileMethod(TypeDefinition type) {
			foreach (var method in type.Methods) {
				if (!method.IsStatic || method.Body == null)
					continue;
				if (method.Parameters.Count != 6)
					continue;
				if (!DotNetUtils.isMethod(method, "System.UInt32", "(System.UInt64&,System.IntPtr,System.IntPtr,System.UInt32,System.IntPtr&,System.UInt32&)"))
					continue;
				return method;
			}
			return null;
		}
	}
}
