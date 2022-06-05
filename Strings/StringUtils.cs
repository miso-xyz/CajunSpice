using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Security.Cryptography;

namespace CajunSpice
{
    class StringUtils
    {
        public static EncryptionMode GetMethodEncryptionType(MethodDef method)
        {
            for (int x = 0; x < method.Body.Instructions.Count; x++)
            {
                Instruction inst = method.Body.Instructions[x];
                switch (inst.OpCode.Code)
                {
                    /*case Code.Xor:
                        if (method.Body.Instructions[x-1].OpCode == OpCodes.Conv_U1 &&
                            method.Body.Instructions[x-2].OpCode == OpCodes.Sub &&
                            method.Body.Instructions[x-3].IsLdcI4() &&
                            method.Body.Instructions[x+1].OpCode == OpCodes.Conv_U1 &&
                            method.Body.Instructions[x+2].OpCode == OpCodes.Stelem_I1)
                        {
                            return EncryptionMode.Xor;
                        }
                        break;
                    case Code.Newarr:
                        if (method.Body.Instructions[x+1].IsStloc() &&
                            method.Body.Instructions[x+2].IsLdarg() &&
                            method.Body.Instructions[x+3].IsLdloc() &&
                            method.Body.Instructions[x+3].GetLocal(method.Body.Variables) == method.Body.Instructions[x+1].GetLocal(method.Body.Variables) &&
                            (method.Body.Instructions[x + 4].OpCode == OpCodes.Call || method.Body.Instructions[x+4].OpCode == OpCodes.Callvirt) &&
                            method.Body.Instructions[x + 4].Operand is MethodDef)
                        {
                            return EncryptionMode.EnhancedXor;
                        }
                        break;*/
                    case Code.Call:
                    case Code.Callvirt:
                        if (inst.Operand is MemberRef)
                        {
                            if (IsASMNameKeyGenerator(method.Body.Instructions, x))
                            {
                                if (x - 2 >= 0 && InstructionHelper.isCall(method.Body.Instructions[x - 2]))
                                {
                                    return EncryptionMode.EnhancedXor;
                                }
                                return EncryptionMode.Xor;
                            }
                            if (Utils.CompareMemberRef(inst, typeof(ICryptoTransform), "TransformFinalBlock", new Type[] { typeof(byte[]), typeof(int), typeof(int) }) &&
                                method.Body.Instructions[x-1].OpCode == OpCodes.Sub &&
                                method.Body.Instructions[x-3].OpCode == OpCodes.Conv_I4 &&
                                method.Body.Instructions[x-8].OpCode == OpCodes.Castclass &&
                                method.Body.Instructions[x-8].Operand is TypeRef &&
                                Utils.CompareTypeRef(method.Body.Instructions[x-8], typeof(ICryptoTransform)) &&
                                method.Body.Instructions[x+1].OpCode == OpCodes.Ret)
                            {
                                return EncryptionMode.TripleDES;
                            }
                        }
                        break;
                }
            }
            return EncryptionMode.Unknown;
        }

        public enum EncryptionMode : int
        {
            None, Hidden, Xor, TripleDES, EnhancedXor, Unknown = -1
        }

        public static bool isReverseArrayCall(Instruction inst) { return InstructionHelper.isCall(inst) && Utils.CompareMemberRef(inst, typeof(Array), "Reverse", new Type[] { typeof(Array) }); }
        public static bool IsASMNameKeyGenerator(IList<Instruction> insts, int startIndex)
        {
            Dictionary<string, Type[]> asmKeyNameGen = new Dictionary<string, Type[]>
            {
                { typeof(Encoding).FullName + "." + "get_UTF8", new Type[] {} },
                { typeof(Assembly).FullName + "." + "GetExecutingAssembly", new Type[] {} },
                { typeof(Assembly).FullName + "." + "get_FullName", new Type[] {} },
                //{ typeof(object).FullName + "." + "ToString", new Type[] {} },
                { typeof(string).FullName + "." + "ToLower", new Type[] {} },
                { typeof(Encoding).FullName + "." + "GetBytes", new Type[] { typeof(string) } }
            };
            List<string> keyNames = asmKeyNameGen.Keys.ToList();
            for (int x = 0; x < asmKeyNameGen.Keys.Count; x++)
            {
                Instruction inst = insts[startIndex + x];
                switch (inst.OpCode.Code)
                {
                    case Code.Callvirt:
                    case Code.Call:
                        if (inst.Operand is MemberRef)
                        {
                            MemberRef method = inst.Operand as MemberRef;
                            string methodFullPath = method.DeclaringType.FullName + "." + method.Name;
                            int keyNameIndex = keyNames.IndexOf(methodFullPath);
                            if (keyNameIndex != -1 && keyNameIndex - x == 0)
                            {
                                break;
                            }
                            return false;
                        }
                        return false;
                }
            }
            return true;
        }

        public static bool IsStringType(TypeDef stringType, out byte[] data, out Encoding encoding, out EncryptionMode encryptionType)
        {
            data = null;
            encryptionType = EncryptionMode.Unknown;
            encoding = null;
            foreach (MethodDef method in stringType.FindConstructors())
            {
                if (!method.HasBody) { continue; }
                int initArrayInstIndex = -1;
                for (int x = 0; x < method.Body.Instructions.Count; x++)
                {
                    Instruction inst = method.Body.Instructions[x];
                    switch (inst.OpCode.Code)
                    {
                        case Code.Stsfld:
                            if (initArrayInstIndex == x - 2)
                            {
                                if (InstructionHelper.isCall(method.Body.Instructions[x - 1]) && method.Body.Instructions[x - 1].Operand is MethodDef)
                                {
                                    encryptionType = GetMethodEncryptionType(method.Body.Instructions[x - 1].Operand as MethodDef);
                                }
                            }
                            else if (initArrayInstIndex == x-1) { encryptionType = EncryptionMode.Hidden; }
                            break;
                        case Code.Call:
                            if (inst.Operand is MemberRef &&
                                Utils.CompareMemberRef(inst, typeof(RuntimeHelpers), "InitializeArray", new Type[] { typeof(Array), typeof(RuntimeFieldHandle) }) &&
                                method.Body.Instructions[x - 1].OpCode == OpCodes.Ldtoken &&
                                method.Body.Instructions[x - 3].OpCode == OpCodes.Newarr)
                            {
                                if (initArrayInstIndex == -1) { initArrayInstIndex = x; } 
                                data = ((FieldDef)method.Body.Instructions[x - 1].Operand).InitialValue;
                            }
                            if (encoding == null &&
                                inst.Operand is MemberRef &&
                                ((MemberRef)inst.Operand).GetDeclaringTypeFullName() == typeof(Encoding).FullName)
                            {
                                encoding = Encoding.GetEncoding(((MemberRef)inst.Operand).Name.Replace("get_", null));
                            }
                            break;
                    }
                    if (data != null && encoding != null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}