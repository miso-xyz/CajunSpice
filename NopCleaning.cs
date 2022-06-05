using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet.Emit;
using dnlib.DotNet;

namespace CajunSpice
{
    class NopCleaning
    {
        public static void CleanASM(ModuleDefMD asm)
        {
            foreach (TypeDef type in Utils.GetAllTypes(asm))
            {
                CleanType(type);
            }
        }
        public static void CleanType(TypeDef type)
        {
            foreach (MethodDef method in type.Methods)
            {
                CleanMethod(method);
            }
        }
        public static void CleanMethod(MethodDef method)
        {
            RemoveUselessNops(method);
            RemoveUselessBRs(method);
            RemoveUselessLocals(method);
        }

        public static void RemoveUselessBRs(MethodDef method)
        {
            for (int x = 0; x < method.Body.Instructions.Count; x++)
            {
                Instruction inst = method.Body.Instructions[x];
                if (inst.OpCode == OpCodes.Br ||
                    inst.OpCode == OpCodes.Br_S ||
                    inst.OpCode == OpCodes.Brtrue ||
                    inst.OpCode == OpCodes.Brtrue_S ||
                    inst.OpCode == OpCodes.Brfalse ||
                    inst.OpCode == OpCodes.Brfalse_S)
                {
                    Instruction jmpInst = (Instruction)inst.Operand;
                    if (jmpInst == method.Body.Instructions[x + 1]) { method.Body.Instructions.RemoveAt(x); x--; }
                }
            }
        }

        public static void RemoveUselessLocals(MethodDef method)
        {
            bool[] usedLocs = new bool[method.Body.Variables.Count];
            for (int x = 0; x < method.Body.Variables.Count; x++)
            {
                Local loc = method.Body.Variables[x];
                if (loc.Type is GenericMVar || loc.Type is GenericVar)
                {
                    usedLocs[loc.Index] = true;
                }
            }
            for (int x = 0; x < method.Body.Instructions.Count; x++)
            {
                if (method.DeclaringType.Name == "Resources" && Utils.IsPropertyMethod(method.DeclaringType, method)) { int.Parse("0"); }
                Instruction inst = method.Body.Instructions[x];
                if (inst.IsStloc() || inst.IsLdloc())
                {
                    Local instLoc = null;
                    try
                    {
                        instLoc = method.Body.Variables[Utils.GetLocalIndex(inst)];
                    }
                    catch (ArgumentOutOfRangeException) { method.Body.Instructions.RemoveAt(x); x--; continue; }
                    switch (method.Body.Instructions[x-1].OpCode.Code)
                    {
                        case Code.Ldfld:
                        case Code.Ldsfld:
                        case Code.Ldsflda:
                        case Code.Call:
                        case Code.Ldftn:
                        case Code.Callvirt:
                            if (method.Body.Instructions.Count >= x+2 &&
                                method.Body.Instructions[x + 1].IsLdloc() &&
                                method.Body.Instructions[x + 2].OpCode == OpCodes.Ret)
                            {
                                method.Body.Instructions.RemoveAt(x);
                                method.Body.Instructions.RemoveAt(x);
                                //x--;
                                continue;
                            }
                            break;
                    }
                    usedLocs[instLoc.Index] = true;
                }
            }
            for (int x = 0; x < method.Body.Variables.Count; x++)
            {
                if (!usedLocs[x]) { method.Body.Variables[x].Type = new Importer(method.Module).ImportAsTypeSig(typeof(object)); method.Body.Variables[x].Name = "CajunSpice - JUNK"; /*method.Body.Variables.RemoveAt(x);*/ }
            }
        }

        public static void RemoveUselessNops(MethodDef method)
        {
            for (int x = 0; x < method.Body.Instructions.Count(); x++)
            {
                Instruction inst = method.Body.Instructions[x];
                if (inst.OpCode == OpCodes.Nop &&
                    !IsNopBranchTarget(method, inst) &&
                    !IsNopSwitchTarget(method, inst) &&
                    !IsNopExceptionHandlerTarget(method, inst))
                {
                    method.Body.Instructions.Remove(inst);
                    x--;
                }
            }
        }

        private static bool IsNopBranchTarget(MethodDef method, Instruction nopInstr)
        {
            var instr = method.Body.Instructions;
            for (int i = 0; i < instr.Count; i++)
            {
                if (instr[i].OpCode.OperandType == OperandType.InlineBrTarget || instr[i].OpCode.OperandType == OperandType.ShortInlineBrTarget && instr[i].Operand != null)
                {
                    Instruction instruction2 = (Instruction)instr[i].Operand;
                    if (instruction2 == nopInstr)
                        return true;
                }
            }
            return false;
        }

        private static bool IsNopSwitchTarget(MethodDef method, Instruction nopInstr)
        {
            var instr = method.Body.Instructions;
            for (int i = 0; i < instr.Count; i++)
            {
                if (instr[i].OpCode.OperandType == OperandType.InlineSwitch && instr[i].Operand != null)
                {
                    Instruction[] source = (Instruction[])instr[i].Operand;
                    if (source.Contains(nopInstr))
                        return true;
                }
            }
            return false;
        }

        private static bool IsNopExceptionHandlerTarget(MethodDef method, Instruction nopInstr)
        {
            bool result;
            if (!method.Body.HasExceptionHandlers)
                result = false;
            else
            {
                var exceptionHandlers = method.Body.ExceptionHandlers;
                foreach (var exceptionHandler in exceptionHandlers)
                {
                    if (exceptionHandler.FilterStart == nopInstr ||
                        exceptionHandler.HandlerEnd == nopInstr ||
                        exceptionHandler.HandlerStart == nopInstr ||
                        exceptionHandler.TryEnd == nopInstr ||
                        exceptionHandler.TryStart == nopInstr)
                        return true;
                }
                result = false;
            }
            return result;
        }

        public static void RemoveUnusedObjects(ModuleDefMD asm)
        {
            List<MethodDef> methods = new List<MethodDef>();
            List<FieldDef> fields = new List<FieldDef>();
            List<TypeDef> typesList = new List<TypeDef>();

            typesList.Add(asm.EntryPoint.DeclaringType);
            methods.Add(asm.EntryPoint);

            TypeDef[] types = Utils.GetAllTypes(asm),
                caTypeDefs = Utils.GetTypeDefAttributes(asm);
            for (int x = 0; x < types.Length; x++)
            {
                TypeDef type = types[x];
                if (!type.HasMethods && !type.IsGlobalModuleType && caTypeDefs.Contains(type)) { asm.Types.Remove(type); types = Utils.GetAllTypes(asm); continue; }
                for (int y = 0; y < type.Methods.Count; y++)
                {
                    MethodDef method = type.Methods[y];
                    if (method.IsConstructor) { methods.Add(method); typesList.Add(method.DeclaringType); }
                    if (!method.HasBody) { type.Methods.RemoveAt(y); y--; continue; }
                    for (int z = 0; z < method.Body.Instructions.Count; z++)
                    {
                        Instruction inst = method.Body.Instructions[z];
                        switch (inst.OpCode.Code)
                        {
                            case Code.Ldfld:
                            case Code.Ldsfld:
                            case Code.Stsfld:
                            case Code.Ldsflda:
                                if (inst.Operand is FieldDef) { if (!fields.Contains((FieldDef)inst.Operand)) { typesList.Add(((FieldDef)inst.Operand).DeclaringType); fields.Add((FieldDef)inst.Operand); } }
                                break;
                            case Code.Ldftn:
                            case Code.Call:
                            case Code.Callvirt:
                                if (inst.Operand is MethodDef)
                                {
                                    if (ProxyMethodTree.IsProxyMethod((MethodDef)inst.Operand)) { type.Methods.Remove(method); y--; break; }
                                    if (!methods.Contains((MethodDef)inst.Operand)) { methods.Add((MethodDef)inst.Operand); typesList.Add(((MethodDef)inst.Operand).DeclaringType); }
                                }
                                break;
                            case Code.Newobj:
                            case Code.Newarr:
                                if (inst.Operand is TypeDef) { if (!typesList.Contains((TypeDef)inst.Operand)) { typesList.Add((TypeDef)inst.Operand); } }
                                break;
                        }
                    }
                }
            }
            types = Utils.GetAllTypes(asm);
            for (int x = 0; x < types.Length; x++)
            {
                TypeDef type = types[x];
                for (int y = 0; y < type.Methods.Count; y++)
                {
                    MethodDef method = type.Methods[y];
                    if (!methods.Contains(method) && !Utils.IsPropertyMethod(method.DeclaringType, method)) { type.Methods.RemoveAt(y); y--; }
                }
                for (int y = 0; y < type.Fields.Count; y++)
                {
                    FieldDef field = type.Fields[y];
                    if (!fields.Contains(field)) { type.Fields.RemoveAt(y); y--; }
                }
                if (!typesList.Contains(type) && !type.IsGlobalModuleType && !type.HasMethods && caTypeDefs.Contains(type)) { if (type.DeclaringType != null) { type.DeclaringType.NestedTypes.Remove(type); } else { asm.Types.Remove(type); } types = Utils.GetAllTypes(asm); continue; }
            }
        }
    }
}
