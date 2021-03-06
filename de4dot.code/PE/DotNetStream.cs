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

using System.IO;

namespace de4dot.PE {
	class DotNetStream : IFileLocation {
		public string name;
		public uint fileOffset;
		public uint length;

		public uint Offset {
			get { return fileOffset; }
		}

		public uint Length {
			get { return length; }
		}

		public DotNetStream(string name, uint fileOffset, uint length) {
			this.name = name;
			this.fileOffset = fileOffset;
			this.length = length;
		}

		public override string ToString() {
			return string.Format("{0:X8} {1:X8} {2}", fileOffset, length, name);
		}
	}
}
