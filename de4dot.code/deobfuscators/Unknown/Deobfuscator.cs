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

using System.Text.RegularExpressions;

namespace de4dot.deobfuscators.Unknown {
	class DeobfuscatorInfo : DeobfuscatorInfoBase {
		public const string THE_NAME = "Unknown";
		public const string THE_TYPE = "un";
		public DeobfuscatorInfo()
			: base() {
		}

		public override string Name {
			get { return THE_NAME; }
		}

		public override string Type {
			get { return THE_TYPE; }
		}

		public override IDeobfuscator createDeobfuscator() {
			return new Deobfuscator(new Deobfuscator.Options {
				RenameResourcesInCode = false,
				ValidNameRegex = validNameRegex.get(),
			});
		}
	}

	class Deobfuscator : DeobfuscatorBase {
		string obfuscatorName;

		internal class Options : OptionsBase {
		}

		public override string Type {
			get { return DeobfuscatorInfo.THE_TYPE; }
		}

		public override string TypeLong {
			get { return DeobfuscatorInfo.THE_NAME; }
		}

		public override string Name {
			get { return obfuscatorName ?? "Unknown Obfuscator"; }
		}

		public Deobfuscator(Options options)
			: base(options) {
		}

		void setName(string name) {
			if (obfuscatorName == null && name != null)
				obfuscatorName = name;
		}

		public override int earlyDetect() {
			setName(earlyScanTypes());
			return obfuscatorName != null ? 1 : 0;
		}

		string earlyScanTypes() {
			foreach (var type in module.Types) {
				if (type.FullName == "ConfusedByAttribute")
					return "Confuser";
			}
			return null;
		}

		protected override int detectInternal() {
			setName(scanTypes());
			return 1;
		}

		protected override void scanForObfuscator() {
		}

		string scanTypes() {
			foreach (var type in module.Types) {
				if (type.FullName == "BabelAttribute" || type.FullName == "BabelObfuscatorAttribute")
					return "Babel Obfuscator";
				if (type.Namespace == "___codefort")
					return "CodeFort";
				if (type.FullName == "____KILL")
					return "DeployLX CodeVeil";
				if (type.FullName.Contains("ObfuscatedByGoliath"))
					return "Goliath .NET Obfuscator";
				if (type.FullName == "ZYXDNGuarder")
					return "DNGuard HVM";
				if (type.FullName == "InfaceMaxtoCode")
					return "MaxtoCode";
				if (type.Name.Contains("();\t"))
					return "Manco .NET Obfuscator";
				if (Regex.IsMatch(type.FullName, @"^EMyPID_\d+_$"))
					return "BitHelmet Obfuscator";
				if (type.FullName == "NineRays.Decompiler.NotDecompile")
					return "Spices.Net Obfuscator";
				if (type.FullName == "YanoAttribute")
					return "Yano Obfuscator";
			}
			return null;
		}
	}
}
