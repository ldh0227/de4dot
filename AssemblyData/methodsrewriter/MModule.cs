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
using Mono.Cecil;
using de4dot.blocks;

namespace AssemblyData.methodsrewriter {
	class MModule {
		public Module module;
		public ModuleDefinition moduleDefinition;
		Dictionary<TypeReferenceKey, MType> typeReferenceToType = new Dictionary<TypeReferenceKey, MType>();
		Dictionary<int, MType> tokenToType = new Dictionary<int, MType>();
		Dictionary<int, MMethod> tokenToGlobalMethod;
		Dictionary<int, MField> tokenToGlobalField;
		TypeDefinition moduleType;

		public MModule(Module module, ModuleDefinition moduleDefinition) {
			this.module = module;
			this.moduleDefinition = moduleDefinition;
			initTokenToType();
		}

		void initTokenToType() {
			var tmpTokenToType = new Dictionary<int, Type>();
			var tmpTokenToTypeDefinition = new Dictionary<int, TypeDefinition>();
			foreach (var t in module.GetTypes())
				tmpTokenToType[t.MetadataToken] = t;
			foreach (var t in moduleDefinition.GetTypes()) {
				tmpTokenToTypeDefinition[t.MetadataToken.ToInt32()] = t;
				if (moduleType == null && t.FullName == "<Module>")
					moduleType = t;
			}
			foreach (var token in tmpTokenToType.Keys) {
				var mtype = new MType(tmpTokenToType[token], tmpTokenToTypeDefinition[token]);
				tokenToType[token] = mtype;
				typeReferenceToType[new TypeReferenceKey(mtype.typeDefinition)] = mtype;
			}
		}

		public MType getType(TypeReference typeReference) {
			MType type;
			typeReferenceToType.TryGetValue(new TypeReferenceKey(typeReference), out type);
			return type;
		}

		public MMethod getMethod(MethodReference methodReference) {
			var type = getType(methodReference.DeclaringType);
			if (type != null)
				return type.getMethod(methodReference);
			if (!MemberReferenceHelper.compareTypes(moduleType, methodReference.DeclaringType))
				return null;

			initGlobalMethods();
			foreach (var method in tokenToGlobalMethod.Values) {
				if (MemberReferenceHelper.compareMethodReference(methodReference, method.methodDefinition))
					return method;
			}

			return null;
		}

		public MField getField(FieldReference fieldReference) {
			var type = getType(fieldReference.DeclaringType);
			if (type != null)
				return type.getField(fieldReference);
			if (!MemberReferenceHelper.compareTypes(moduleType, fieldReference.DeclaringType))
				return null;

			initGlobalFields();
			foreach (var field in tokenToGlobalField.Values) {
				if (MemberReferenceHelper.compareFieldReference(fieldReference, field.fieldDefinition))
					return field;
			}

			return null;
		}

		public MMethod getMethod(MethodBase method) {
			if (method.Module != module)
				throw new ApplicationException("Not our module");
			if (method.DeclaringType == null)
				return getGlobalMethod(method);
			var type = tokenToType[method.DeclaringType.MetadataToken];
			return type.getMethod(method.MetadataToken);
		}

		public MMethod getGlobalMethod(MethodBase method) {
			initGlobalMethods();
			return tokenToGlobalMethod[method.MetadataToken];
		}

		void initGlobalMethods() {
			if (tokenToGlobalMethod != null)
				return;
			tokenToGlobalMethod = new Dictionary<int, MMethod>();

			var tmpTokenToGlobalMethod = new Dictionary<int, MethodInfo>();
			var flags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			foreach (var m in module.GetMethods(flags))
				tmpTokenToGlobalMethod[m.MetadataToken] = m;
			foreach (var m in moduleType.Methods) {
				if (m.Name == ".cctor")	//TODO: Use module.GetMethod(token) to get .cctor method
					continue;
				var token = m.MetadataToken.ToInt32();
				tokenToGlobalMethod[token] = new MMethod(tmpTokenToGlobalMethod[token], m);
			}
		}

		void initGlobalFields() {
			if (tokenToGlobalField != null)
				return;
			tokenToGlobalField = new Dictionary<int, MField>();

			var tmpTokenToGlobalField = new Dictionary<int, FieldInfo>();
			var flags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			foreach (var f in module.GetFields(flags))
				tmpTokenToGlobalField[f.MetadataToken] = f;
			foreach (var f in moduleType.Fields) {
				var token = f.MetadataToken.ToInt32();
				tokenToGlobalField[token] = new MField(tmpTokenToGlobalField[token], f);
			}
		}

		public override string ToString() {
			return moduleDefinition.FullyQualifiedName;
		}
	}
}
