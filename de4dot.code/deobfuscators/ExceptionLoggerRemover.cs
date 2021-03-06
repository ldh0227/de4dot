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

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using de4dot.blocks;

namespace de4dot.deobfuscators {
	class ExceptionLoggerRemover {
		Dictionary<MethodReference, bool> exceptionLoggerMethods = new Dictionary<MethodReference, bool>();

		public int NumRemovedExceptionLoggers { get; set; }

		public void add(MethodDefinition exceptionLogger) {
			exceptionLoggerMethods[exceptionLogger] = true;
		}

		bool find(Blocks blocks, out TryBlock tryBlock) {
			tryBlock = null;

			foreach (var bb in blocks.MethodBlocks.BaseBlocks) {
				tryBlock = bb as TryBlock;
				if (tryBlock == null)
					continue;
				if (tryBlock.TryHandlerBlocks.Count != 1)
					continue;
				var catchBlock = tryBlock.TryHandlerBlocks[0];
				if (catchBlock.HandlerType != ExceptionHandlerType.Catch ||
					!MemberReferenceHelper.verifyType(catchBlock.CatchType, "mscorlib", "System.Exception")) {
					continue;
				}
				if (catchBlock.BaseBlocks.Count != 1)
					continue;
				var handlerBlock = catchBlock.BaseBlocks[0] as HandlerBlock;
				if (handlerBlock == null)
					continue;

				int calls = 0;
				Instr callInstr = null;
				bool failed = false;
				foreach (var bb2 in handlerBlock.BaseBlocks) {
					var block = bb2 as Block;
					if (block == null) {
						failed = true;
						break;
					}
					foreach (var instr in block.Instructions) {
						switch (instr.OpCode.Code) {
						case Code.Call:
						case Code.Calli:
						case Code.Callvirt:
							calls++;
							callInstr = instr;
							break;
						}
					}
				}
				if (failed || calls != 1 || callInstr.OpCode.Code != Code.Call)
					continue;
				var calledMethod = callInstr.Operand as MethodReference;
				if (calledMethod == null)
					continue;
				if (!exceptionLoggerMethods.ContainsKey(calledMethod))
					continue;

				return true;
			}

			return false;
		}

		public bool remove(Blocks blocks) {
			if (exceptionLoggerMethods.Count == 0)
				return false;

			TryBlock tryBlock;
			if (!find(blocks, out tryBlock))
				return false;

			blocks.MethodBlocks.removeTryBlock(tryBlock);
			NumRemovedExceptionLoggers++;
			return true;
		}
	}
}
