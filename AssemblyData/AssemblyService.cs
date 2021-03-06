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
using System.Reflection;
using System.Threading;

namespace AssemblyData {
	public class AssemblyService : MarshalByRefObject, IAssemblyService {
		IStringDecrypter stringDecrypter = null;
		ManualResetEvent exitEvent = new ManualResetEvent(false);
		Assembly assembly = null;
		AssemblyResolver assemblyResolver = new AssemblyResolver();

		public void doNothing() {
		}

		void checkStringDecrypter() {
			if (stringDecrypter == null)
				throw new ApplicationException("setStringDecrypterType() hasn't been called yet.");
		}

		void checkAssembly() {
			if (assembly == null)
				throw new ApplicationException("loadAssembly() hasn't been called yet.");
		}

		public void loadAssembly(string filename) {
			if (assembly != null)
				throw new ApplicationException("Only one assembly can be explicitly loaded");
			try {
				assembly = assemblyResolver.load(filename);
			}
			catch (BadImageFormatException) {
				throw new ApplicationException(string.Format("Could not load assembly {0}. Maybe it's 32-bit or 64-bit only?", filename));
			}
		}

		public void setStringDecrypterType(StringDecrypterType type) {
			if (stringDecrypter != null)
				throw new ApplicationException("StringDecrypterType already set");

			switch (type) {
			case StringDecrypterType.Delegate:
				stringDecrypter = new DelegateStringDecrypter();
				break;

			case StringDecrypterType.Emulate:
				stringDecrypter = new EmuStringDecrypter();
				break;

			default:
				throw new ApplicationException(string.Format("Unknown StringDecrypterType {0}", type));
			}
		}

		public int defineStringDecrypter(int methodToken) {
			checkStringDecrypter();
			var methodInfo = findMethod(methodToken);
			if (methodInfo == null)
				throw new ApplicationException(string.Format("Could not find method {0:X8}", methodToken));
			if (methodInfo.ReturnType != typeof(string))
				throw new ApplicationException(string.Format("Method return type must be string: {0}", methodInfo));
			return stringDecrypter.defineStringDecrypter(methodInfo);
		}

		public object[] decryptStrings(int stringDecrypterMethod, object[] args) {
			checkStringDecrypter();
			foreach (var arg in args)
				SimpleData.unpack((object[])arg);
			return SimpleData.pack(stringDecrypter.decryptStrings(stringDecrypterMethod, args));
		}

		public void exit() {
			exitEvent.Set();
		}

		public void waitExit() {
			exitEvent.WaitOne();
		}

		public override object InitializeLifetimeService() {
			return null;
		}

		MethodInfo findMethod(int methodToken) {
			checkAssembly();

			foreach (var module in assembly.GetModules()) {
				var method = module.ResolveMethod(methodToken) as MethodInfo;
				if (method != null)
					return method;
			}

			return null;
		}
	}
}
