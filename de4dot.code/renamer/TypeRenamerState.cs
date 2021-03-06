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

namespace de4dot.renamer {
	class TypeRenamerState {
		ExistingNames existingNames;
		Dictionary<string, string> namespaceToNewName;
		NameCreator createNamespaceName;
		public ITypeNameCreator globalTypeNameCreator;
		public ITypeNameCreator internalTypeNameCreator;

		public TypeRenamerState() {
			existingNames = new ExistingNames();
			namespaceToNewName = new Dictionary<string, string>(StringComparer.Ordinal);
			createNamespaceName = new NameCreator("ns");
			globalTypeNameCreator = new GlobalTypeNameCreator(existingNames);
			internalTypeNameCreator = new TypeNameCreator(existingNames);
		}

		public void addTypeName(string name) {
			existingNames.add(name);
		}

		public string getTypeName(string oldName, string newName) {
			return existingNames.getName(oldName, new NameCreator2(newName));
		}

		public string createNamespace(string ns) {
			string newName;
			if (namespaceToNewName.TryGetValue(ns, out newName))
				return newName;
			return namespaceToNewName[ns] = createNamespaceName.create();
		}
	}
}
