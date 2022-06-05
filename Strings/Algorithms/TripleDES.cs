using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace CajunSpice
{
    class TripleDES
    {
        public class Profile
        {
            public static Profile FromType(TypeDef type)
            {
                Profile result = new Profile();
                foreach (MethodDef method in type.Methods)
                {
                    if (!method.HasBody) { continue; }
                    bool castClassMethod = false;
                    Local ivLoc = null, keyLoc = null;
                    for (int x = 0; x < method.Body.Instructions.Count; x++)
                    {
                        Instruction inst = method.Body.Instructions[x];
                        if (inst.IsStloc())
                        {
                            Local tempLoc = inst.GetLocal(method.Body.Variables);
                            if ((method.Body.Instructions[x - 1].OpCode == OpCodes.Call || method.Body.Instructions[x - 1].OpCode == OpCodes.Callvirt) &&
                                method.Body.Instructions[x - 1].Operand is MemberRef &&
                                Utils.CompareMemberRef(method.Body.Instructions[x - 1], typeof(RuntimeHelpers), "InitializeArray", new Type[] { typeof(Array), typeof(RuntimeFieldHandle) }) &&
                                method.Body.Instructions[x - 2].OpCode == OpCodes.Ldtoken)
                            {
                                keyLoc = tempLoc;
                            }
                            else if (x - 5 >= 0 && StringUtils.IsASMNameKeyGenerator(method.Body.Instructions, x - 5))
                            {
                                ivLoc = tempLoc;
                            }
                            continue;
                        }
                        if (inst.IsLdloc())
                        {
                            if (StringUtils.isReverseArrayCall(method.Body.Instructions[x + 1]))
                            {
                                if (inst.GetLocal(method.Body.Variables) == ivLoc)
                                {
                                    result.ArrayReversed_iv = true;
                                }
                                if (inst.GetLocal(method.Body.Variables) == keyLoc)
                                {
                                    result.ArrayReversed_key = true;
                                }
                                continue;
                            }
                        }
                        if (inst.IsLdcI4() && castClassMethod)
                        {
                            if (InstructionHelper.isCall(method.Body.Instructions[x + 2]) &&
                                method.Body.Instructions[x + 1].OpCode == OpCodes.Sub)
                            {
                                result.TFB_inputCountSubValue = inst.GetLdcI4Value();
                            }
                            if (method.Body.Instructions[x - 2].OpCode == OpCodes.Castclass)
                            {
                                result.TFB_inputOffset = inst.GetLdcI4Value();
                            }
                        }
                        switch (inst.OpCode.Code)
                        {
                            case Code.Castclass:
                                if (inst.Operand is TypeRef && Utils.CompareTypeRef(inst, typeof(ICryptoTransform)) && method.Body.Instructions[x+2].IsLdcI4())
                                {
                                    castClassMethod = true;
                                    if (x - 2 >= 0 &&
                                       (method.Body.Instructions[x - 2].OpCode == OpCodes.Call || method.Body.Instructions[x - 2].OpCode == OpCodes.Callvirt) &&
                                        method.Body.Instructions[x - 2].Operand is MemberRef &&
                                        Utils.CompareMemberRef(method.Body.Instructions[x - 2], typeof(Array), "Reverse", new Type[] { typeof(Array) }))
                                    {
                                        result.ArrayReversed_source = true;
                                    }
                                }
                                break;
                            case Code.Call:
                            case Code.Callvirt:
                                if (inst.Operand is MemberRef)
                                {
                                    if (x - 6 >= 0 &&
                                        Utils.CompareMemberRef(inst, typeof(RuntimeHelpers), "InitializeArray", new Type[] { typeof(Array), typeof(RuntimeFieldHandle) }) &&
                                        method.Body.Instructions[x - 1].OpCode == OpCodes.Ldtoken &&
                                        method.Body.Instructions[x - 3].OpCode == OpCodes.Newarr &&
                                        method.Body.Instructions[x - 5].IsStloc() &&
                                        method.Body.Instructions[x - 6].OpCode == OpCodes.Newobj)
                                    {
                                        result.DecryptionKey = (method.Body.Instructions[x - 1].Operand as FieldDef).InitialValue;
                                    }
                                }
                                break;
                        }
                    }
                }
                return result;
            }

            public int TFB_inputOffset { get; set; }
            public int TFB_inputCountSubValue { get; set; }

            public byte[] DecryptionKey { get; set; }

            public bool ArrayReversed_source { get; set; }
            public bool ArrayReversed_key { get; set; }
            public bool ArrayReversed_iv { get; set; }
        }

        public Profile Parameters { get; set; }

        public TripleDES(Profile profile) { Parameters = profile; }

        public static TripleDES FromType(TypeDef type)
        {
            return new TripleDES(Profile.FromType(type));
        }

        public byte[] Fix(byte[] src, ModuleDefMD asm)
        {
            TripleDESCryptoServiceProvider tripleDES = new TripleDESCryptoServiceProvider();
            byte[] iv = Encoding.UTF8.GetBytes(asm.Assembly.FullName.ToLower());
            if (Parameters.ArrayReversed_key) { Array.Reverse(Parameters.DecryptionKey); }
            if (Parameters.ArrayReversed_source) { Array.Reverse(src); }
            if (Parameters.ArrayReversed_iv) { Array.Reverse(iv); }
            return tripleDES
                .CreateDecryptor(Parameters.DecryptionKey, iv)
                .TransformFinalBlock(src, Parameters.TFB_inputOffset, src.Length - Parameters.TFB_inputCountSubValue);
        }
    }
}
