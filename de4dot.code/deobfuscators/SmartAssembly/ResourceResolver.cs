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
using System.IO;
using Mono.Cecil;
using de4dot.blocks;

namespace de4dot.deobfuscators.SmartAssembly {
	class ResourceResolver {
		ModuleDefinition module;
		AssemblyResolver assemblyResolver;
		ResourceResolverInfo resourceResolverInfo;
		bool mergedIt = false;

		public ResourceResolver(ModuleDefinition module, AssemblyResolver assemblyResolver, ResourceResolverInfo resourceResolverInfo) {
			this.module = module;
			this.assemblyResolver = assemblyResolver;
			this.resourceResolverInfo = resourceResolverInfo;
		}

		public bool canDecryptResource() {
			return assemblyResolver.canDecryptResource(resourceResolverInfo.ResourceInfo);
		}

		public EmbeddedAssemblyInfo mergeResources() {
			if (mergedIt)
				return null;

			var info = resourceResolverInfo.ResourceInfo;
			if (info == null)
				return null;

			DeobUtils.decryptAndAddResources(module, info.resourceName, () => assemblyResolver.removeDecryptedResource(info));
			mergedIt = true;
			return info;
		}
	}
}
