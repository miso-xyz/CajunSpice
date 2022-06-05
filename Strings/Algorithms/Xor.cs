using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Reflection;

namespace CajunSpice
{
    public class Xor
    {
        public class Profile
        {
            public Profile() { }

            public static Profile FromType(TypeDef type)
            {
                Profile result = new Profile();
                foreach (MethodDef method in type.Methods)
                {
                    if (!method.HasBody) { continue; }
                    Local keyLoc = null, decLoc = null;
                    for (int x = 0; x < method.Body.Instructions.Count; x++)
                    {
                        Instruction inst = method.Body.Instructions[x];
                        switch (inst.OpCode.Code)
                        {
                            case Code.Call:
                                if (inst.Operand is MemberRef)
                                {
                                    //if (Utils.CompareMemberRef(inst, typeof(Array), "Reverse", new Type[] { typeof(Array) })) { reverseArray = true; }
                                    if (StringUtils.IsASMNameKeyGenerator(method.Body.Instructions, x))
                                    {
                                        if (x != 0) { result.SourceReverseArray = StringUtils.isReverseArrayCall(method.Body.Instructions[x - 1]); }
                                        result.KeyReverseArray = StringUtils.isReverseArrayCall(method.Body.Instructions[x + 7]);
                                        keyLoc = method.Body.Instructions[x + 4].GetLocal(method.Body.Variables);
                                    }
                                }
                                break;
                            case Code.Ldelem_U1:
                                if (method.Body.Instructions[x - 1].OpCode == OpCodes.Add &&
                                    method.Body.Instructions[x - 2].IsLdcI4())
                                {
                                    result.SourceByteIndexOffset = method.Body.Instructions[x - 2].GetLdcI4Value();
                                }
                                if (x + 4 < method.Body.Instructions.Count &&
                                    method.Body.Instructions[x + 2].OpCode == OpCodes.Sub &&
                                    method.Body.Instructions[x + 4].OpCode == OpCodes.Xor)
                                {
                                    result.DecryptedByteSubValue = method.Body.Instructions[x + 1].GetLdcI4Value();
                                }
                                break;
                            case Code.Newarr:
                                if (method.Body.Instructions[x - 1].OpCode == OpCodes.Sub)
                                {
                                    decLoc = method.Body.Instructions[x + 1].GetLocal(method.Body.Variables);
                                    result.ArraySizeSubValue = method.Body.Instructions[x - 2].GetLdcI4Value();
                                }
                                break;
                        }
                    }
                }
                return result;
            }

            public bool KeyReverseArray { get; set; }
            public bool SourceReverseArray { get; set; }

            public int ArraySizeSubValue { get; set; }
            public int SourceByteIndexOffset { get; set; }
            public int DecryptedByteSubValue { get; set; }
        }

        public Xor(Profile profile)
        {
            Parameters = profile;
        }

        public Profile Parameters { get; set; }

        public static Xor FromType(TypeDef type) { return new Xor(Profile.FromType(type)); }

        public byte[] Fix(byte[] data, ModuleDefMD asm)
        {
            byte[] asmFullName = Encoding.UTF8.GetBytes(asm.Assembly.FullName.ToLower());
            if (Parameters.KeyReverseArray) { Array.Reverse(asmFullName); }
            if (Parameters.SourceReverseArray) { Array.Reverse(data); }
            byte[] array = new byte[data.Length - Parameters.ArraySizeSubValue];
            int i = 0;
            int num = 0;
            while (i < array.Length)
            {
                if (num >= asmFullName.Length)
                {
                    num = 0;
                }
                array[i] = (byte)(data[i + Parameters.SourceByteIndexOffset] ^ asmFullName[num] - Parameters.DecryptedByteSubValue);
                i++;
                num++;
            }
            return array;
        }
    }
}
