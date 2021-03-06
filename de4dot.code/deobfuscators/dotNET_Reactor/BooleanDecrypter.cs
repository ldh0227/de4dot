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
using Mono.Cecil;
using de4dot.blocks;

namespace de4dot.deobfuscators.dotNET_Reactor {
	class BooleanDecrypter {
		ModuleDefinition module;
		EncryptedResource encryptedResource;
		byte[] fileData;
		byte[] decryptedData;

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

		public BooleanDecrypter(ModuleDefinition module) {
			this.module = module;
			this.encryptedResource = new EncryptedResource(module);
		}

		public BooleanDecrypter(ModuleDefinition module, BooleanDecrypter oldOne) {
			this.module = module;
			this.encryptedResource = new EncryptedResource(module, oldOne.encryptedResource);
		}

		public void find() {
			var additionalTypes = new string[] {
				"System.Boolean",
			};
			foreach (var type in module.Types) {
				if (type.BaseType == null || type.BaseType.FullName != "System.Object")
					continue;
				foreach (var method in type.Methods) {
					if (!method.IsStatic || !method.HasBody)
						continue;
					if (!DotNetUtils.isMethod(method, "System.Boolean", "(System.Int32)"))
						continue;
					if (!encryptedResource.couldBeResourceDecrypter(method, additionalTypes))
						continue;

					encryptedResource.Method = method;
					return;
				}
			}
		}

		public void init(byte[] fileData, ISimpleDeobfuscator simpleDeobfuscator) {
			if (encryptedResource.Method == null)
				return;
			this.fileData = fileData;

			encryptedResource.init(simpleDeobfuscator);
			if (!encryptedResource.FoundResource)
				return;

			Log.v("Adding boolean decrypter. Resource: {0}", Utils.toCsharpString(encryptedResource.Resource.Name));
			decryptedData = encryptedResource.decrypt();
		}

		public bool decrypt(int offset) {
			uint byteOffset = BitConverter.ToUInt32(decryptedData, offset);
			return fileData[byteOffset] == 0x80;
		}
	}
}
