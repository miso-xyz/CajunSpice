using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace CajunSpice
{
    class StringFixer
    {
        public static void Fix(ModuleDefMD asm)
        {
            StringMethod[] stringMethods = GetStringMethods(asm);
            byte[] data = null;
            Encoding enc = Encoding.Default;
            StringUtils.EncryptionMode encMode = StringUtils.EncryptionMode.Unknown;
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Searching for Protected Strings...");
            //int decryptedArrayLength, xorVal1, subXorVal2;
            for (int x = 0; x < stringMethods.Length; x++)
            {
                StringMethod stringMethod = stringMethods[x];
                StringUtils.IsStringType(stringMethod.encField.DeclaringType, out data, out enc, out encMode);
                if (encMode != StringUtils.EncryptionMode.None || encMode != StringUtils.EncryptionMode.Unknown)
                {
                    TypeDef type = stringMethod.encField.DeclaringType;
                    switch (encMode)
                    {
                        case StringUtils.EncryptionMode.EnhancedXor:
                            data = EnhancedXor.FromType(type).Fix(data, asm);
                            break;
                        case StringUtils.EncryptionMode.Xor:
                            data = Xor.FromType(type).Fix(data, asm);
                            break;
                        case StringUtils.EncryptionMode.TripleDES:
                            data = TripleDES.FromType(type).Fix(data, asm);
                            break;
                    }
                    if (data != null) { break; }
                }
            }
            if (data == null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("No String Protection Found!");
                return;
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("String Protection Found!\nProtection: " + Enum.GetName(typeof(StringUtils.EncryptionMode), encMode) + "\n");
            foreach (TypeDef type in Utils.GetAllTypes(asm))
            {
                foreach (MethodDef method in type.Methods)
                {
                    if (!method.HasBody) { continue; }
                    for (int y = 0; y < method.Body.Instructions.Count; y++)
                    {
                        Instruction inst = method.Body.Instructions[y];
                        switch (inst.OpCode.Code)
                        {
                            case Code.Call:
                                StringMethod tempStrMethod;
                                if (inst.Operand is MethodDef && StringMethod.Contains(inst.Operand as MethodDef, stringMethods, out tempStrMethod))
                                {
                                    string ogText = enc.GetString(data, tempStrMethod.index, tempStrMethod.count);
                                    Console.ForegroundColor = ConsoleColor.Cyan;
                                    Console.WriteLine("[Strings]: Recovered '" + ogText + "' at " + method.Name + "::IL_" + inst.Offset.ToString("X4"));
                                    inst.OpCode = OpCodes.Ldstr;
                                    inst.Operand = ogText;
                                }
                                break;
                        }
                    }
                }
            }
        }

        public static StringMethod[] GetStringMethods(ModuleDefMD asm)
        {
            List<StringMethod> result = new List<StringMethod>();
            foreach (TypeDef type in Utils.GetAllTypes(asm))
            {
                foreach (MethodDef method in type.Methods)
                {
                    if (!method.HasBody) { continue; }
                    for (int x = 0; x < method.Body.Instructions.Count; x++)
                    {
                        Instruction inst = method.Body.Instructions[x];
                        switch (inst.OpCode.Code)
                        {
                            case Code.Callvirt:
                                if (inst.Operand is MemberRef &&
                                    Utils.CompareMemberRef(inst, typeof(Encoding), "GetString", new Type[] { typeof(byte[]), typeof(int), typeof(int) }) &&
                                    method.Body.Instructions[x - 1].IsLdcI4() &&
                                    method.Body.Instructions[x - 2].IsLdcI4() &&
                                    method.Body.Instructions[x - 3].OpCode == OpCodes.Ldsfld &&
                                    method.Body.Instructions[x - 4].OpCode == OpCodes.Ldsfld &&
                                    ((FieldDef)method.Body.Instructions[x - 3].Operand).FieldType.FullName == typeof(byte[]).FullName &&
                                    ((FieldDef)method.Body.Instructions[x - 4].Operand).FieldType.FullName == typeof(Encoding).FullName)
                                {
                                    StringMethod strMethod = new StringMethod(
                                        method,
                                        method.Body.Instructions[x - 3].Operand as FieldDef,
                                        method.Body.Instructions[x - 4].Operand as FieldDef,
                                        method.Body.Instructions[x - 2].GetLdcI4Value(),
                                        method.Body.Instructions[x - 1].GetLdcI4Value());
                                    if (!result.Contains(strMethod)) { result.Add(strMethod); }
                                }
                                break;
                        }
                    }
                }
            }
            return result.ToArray();
        }
    }
}
