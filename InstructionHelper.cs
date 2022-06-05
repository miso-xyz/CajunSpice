using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet.Emit;
using dnlib.DotNet;

namespace CajunSpice
{
    class InstructionHelper
    {
        public static bool isCall(Instruction inst)
        {
            return inst.OpCode == OpCodes.Call || inst.OpCode == OpCodes.Callvirt;
        }

        public static Instruction[] GetArgsRefs(int argIndex, MethodDef method)
        {
            List<Instruction> result = new List<Instruction>();
            for (int x = 0; x < method.Body.Instructions.Count; x++)
            {
                Instruction inst = method.Body.Instructions[x];
                if (inst.IsLdarg() || inst.IsStarg())
                {
                    if (int.Parse(inst.OpCode.ToString().Split('_')[2]) == argIndex)
                    {
                        result.Add(inst);
                    }
                }
            }
            return result.ToArray();
        }

        public static Instruction[] GetLocalRefs(Local loc, MethodDef method)
        {
            List<Instruction> result = new List<Instruction>();
            for (int x = 0; x < method.Body.Instructions.Count; x++)
            {
                Instruction inst = method.Body.Instructions[x];
                if (inst.IsStloc() || inst.IsLdloc())
                {
                    if (inst.GetLocal(method.Body.Variables) == loc)
                    {
                        result.Add(inst);
                    }
                }
            }
            return result.ToArray();
        }


        public static bool isBitShift(Instruction inst)
        {
            return inst.OpCode == OpCodes.Shl || inst.OpCode == OpCodes.Shr || inst.OpCode == OpCodes.Shr_Un;
        }

        public static bool isMathOperator(Instruction inst)
        {
            return inst.OpCode == OpCodes.Add || inst.OpCode == OpCodes.Sub || inst.OpCode == OpCodes.Mul || inst.OpCode == OpCodes.Div || inst.OpCode == OpCodes.Neg || inst.OpCode == OpCodes.And || inst.OpCode == OpCodes.Or || inst.OpCode == OpCodes.Xor || isBitShift(inst);
        }

        public static byte[] GetLdtoken(Instruction inst)
        {
            return ((FieldDef)inst.Operand).InitialValue;
        }
    }
}
