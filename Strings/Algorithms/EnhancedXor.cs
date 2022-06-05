using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Reflection;

namespace CajunSpice
{
    public class EnhancedXor
    {
        public class Profile
        {
            public static Profile FromMethod(MethodDef method)
            {
                Profile result = new Profile();
                for (int x = 0; x < method.Body.Instructions.Count; x++)
                {
                    Instruction inst = method.Body.Instructions[x];
                    switch (inst.OpCode.Code)
                    {
                        case Code.Brtrue:
                        case Code.Brtrue_S:
                            if (method.Body.Instructions[x - 1].OpCode == OpCodes.And &&
                                method.Body.Instructions[x - 2].IsLdcI4() &&
                                method.Body.Instructions[x - 4].IsLdcI4() &&
                                method.Body.Instructions[x + 2].IsLdcI4() &&
                                method.Body.Instructions[x - 4].GetLdcI4Value() == method.Body.Instructions[x + 2].GetLdcI4Value())
                            {
                                result.EncryptionIndicatorByteIndex = method.Body.Instructions[x + 2].GetLdcI4Value();
                            }
                            break;
                        case Code.Ret:
                            if (method.Body.Instructions[x - 1].IsLdcI4() &&
                                method.Body.Instructions[x - 2].OpCode == OpCodes.Call &&
                                method.Body.Instructions[x - 2].Operand is MethodDef &&
                                method.Body.Instructions[x - 3].IsLdcI4() &&
                                method.Body.Instructions[x - 3].GetLdcI4Value() == method.Body.Instructions[x - 1].GetLdcI4Value())
                            {
                                result.DecryptedArrayLength = method.Body.Instructions[x - 1].GetLdcI4Value();
                            }
                            break;
                    }
                }
                return result;
            }

            public Profile()
            {
                XorSubValue = DecryptedArrayOffsetStart = -1;
            }

            public int EncryptionIndicatorByteIndex { get;set; }
            public int DecryptedArrayLength { get; set; }

            public int XorSubValue { get; set; }
            public int DecryptedArrayOffsetStart { get; set; }

            public bool KeyArrayReversed { get; set; }
            public bool SourceArrayReversed { get; set; }
        }

        public static EnhancedXor FromType(TypeDef type)
        {
            Profile pr = new Profile();
            foreach (MethodDef method in type.Methods)
            {
                if (!method.HasBody) { continue; }
                for (int x = 0; x < method.Body.Instructions.Count; x++)
                {
                    Instruction inst = method.Body.Instructions[x];
                    switch (inst.OpCode.Code)
                    {
                        case Code.Xor:
                            if (method.Body.Instructions[x - 1].OpCode == OpCodes.Conv_U1 &&
                                method.Body.Instructions[x - 2].OpCode == OpCodes.Sub &&
                                method.Body.Instructions[x - 3].IsLdcI4())
                            {
                                pr.XorSubValue = method.Body.Instructions[x - 3].GetLdcI4Value();
                            }
                            break;
                        case Code.Call:
                        case Code.Callvirt:
                            if (inst.Operand is MemberRef && Utils.CompareMemberRef(inst, typeof(Array), "Reverse", new Type[] { typeof(Array) }))
                            {
                                switch (method.Body.Instructions[x - 3].OpCode.Code)
                                {
                                    case Code.Newarr:
                                        pr.SourceArrayReversed = true;
                                        break;
                                    case Code.Callvirt:
                                        pr.KeyArrayReversed = true;
                                        break;
                                }
                            }
                            break;
                        case Code.Br:
                        case Code.Br_S:
                            if (method.Body.Instructions[x - 1].IsStloc() &&
                                method.Body.Instructions[x - 2].IsLdcI4() &&
                                method.Body.Instructions[x - 3].IsStloc() &&
                                method.Body.Instructions[x - 4].IsLdcI4() &&
                                method.Body.Instructions[x + 1].IsLdloc() &&
                                method.Body.Instructions[x + 2].IsLdloc() &&
                                method.Body.Instructions[x + 4].OpCode == OpCodes.Conv_I4)
                            {
                                pr.DecryptedArrayOffsetStart = method.Body.Instructions[x - 4].GetLdcI4Value();
                            }
                            break;
                    }
                    if (pr.XorSubValue != -1 && pr.DecryptedArrayOffsetStart != -1) { break; }
                }
            }
            foreach (MethodDef method in type.Methods)
            {
                bool exitLoop = false;
                for (int x = 0; x < method.Body.Instructions.Count; x++)
                {
                    Instruction inst = method.Body.Instructions[x];
                    switch (inst.OpCode.Code)
                    {
                        case Code.Ret:
                            if (method.Body.Instructions[x - 1].IsLdcI4() &&
                                method.Body.Instructions[x - 2].OpCode == OpCodes.Call &&
                                method.Body.Instructions[x - 2].Operand is MethodDef &&
                                method.Body.Instructions[x - 3].IsLdcI4() &&
                                method.Body.Instructions[x - 3].GetLdcI4Value() == method.Body.Instructions[x - 1].GetLdcI4Value())
                            {
                                Profile tempPr = Profile.FromMethod(method);
                                tempPr.XorSubValue = pr.XorSubValue;
                                tempPr.DecryptedArrayOffsetStart = pr.DecryptedArrayOffsetStart;
                                tempPr.SourceArrayReversed = pr.SourceArrayReversed;
                                tempPr.KeyArrayReversed = pr.KeyArrayReversed;
                                pr = tempPr;
                                exitLoop = true;
                            }
                            break;
                    }
                }
                if (exitLoop) { break; }
            }
            return new EnhancedXor(pr);
        }

        public EnhancedXor(Profile pr)
        {
            Parameters = pr;
        }

        public Profile Parameters { get; set; }

        public bool isEncrypted(byte[] src)
        {
            return (src[Parameters.EncryptionIndicatorByteIndex] & 1) == 1;
        }

        public byte[] Fix(byte[] src, ModuleDefMD asm)
        {
            byte[] array = new byte[Parameters.DecryptedArrayLength];
            if (Parameters.SourceArrayReversed) { Array.Reverse(src); }
            if ((src[Parameters.EncryptionIndicatorByteIndex] & 0) == 0)
			{
                int offsetNum = 2 * (((src[Parameters.EncryptionIndicatorByteIndex] & 2) == 2) ? 4 : 1) + 1;
                if (isEncrypted(src))
				{
                    array = Decrypt(src, Parameters.EncryptionIndicatorByteIndex + offsetNum, 0);
				}
				else
				{
                    CopyToArray(src, Parameters.EncryptionIndicatorByteIndex + offsetNum, array, 0, Parameters.DecryptedArrayLength);
				}
			}
            // original XOR algorithm
            byte[] asmFullName = Encoding.UTF8.GetBytes(asm.Assembly.FullName.ToLower());
            if (Parameters.KeyArrayReversed) { Array.Reverse(asmFullName); }
            List<byte> unXor = new List<byte>();
            int i = Parameters.DecryptedArrayOffsetStart;
            int num = 0;
            while (i < Parameters.DecryptedArrayLength)
            {
                if (num >= asmFullName.Length)
                {
                    num = 0;
                }
                array[i] ^= (byte)(asmFullName[num] - Parameters.XorSubValue);
                i++;
                num++;
            }
            return array;
        }

        private uint BitShift(byte[] src, int index, int value)
        {
	        if (value == 1)
	        {
		        return (uint)src[index];
	        }
	        if (value == 2)
	        {
		        return (uint)((int)src[index] + ((int)src[index + 1] << 8));
	        }
	        if (value == 3)
	        {
		        return (uint)((int)src[index] + ((int)src[index + 1] << 8) + ((int)src[index + 2] << 16));
	        }
	        if (value != 4)
	        {
		        return 0U;
	        }
	        return (uint)((int)src[index] + ((int)src[index + 1] << 8) + ((int)src[index + 2] << 16) + ((int)src[index + 3] << 24));
        }

        public static void CopyToArray(byte[] src, int srcStartIndex, byte[] result, int resultStartIndex, int length)
        {
	        for (int i = 0; i < length; i++)
	        {
                result[resultStartIndex + i] = src[srcStartIndex + i];
	        }
        }

        private byte[] Decrypt(byte[] src, int startIndex, int resultIndex)
        {
            byte[] result = new byte[Parameters.DecryptedArrayLength];
            int num = startIndex;
            int i = resultIndex;
            int num2 = resultIndex + Parameters.DecryptedArrayLength;
            int num3 = num2 - 3;
            uint num4 = 1U;
            if (i >= num3)
            {
                num += 4;
                while (i < num2)
                {
                    result[i++] = src[num++];
                }
                return result;
            }
            for (; ; )
            {
                if (num4 == 1U)
                {
                    num4 = BitShift(src, num, 4);
                    num += 4;
                }
                if (src.Length <= num) { break; } // was used for debugging, prevents IndexOOB on 'num'
                uint num5 = BitShift(src, num, 4);
                if ((num4 & 1U) == 1U)
                {
                    num4 >>= 1;
                    if ((num5 & 3U) == 0U)
                    {
                        uint num6 = (num5 & 255U) >> 2;
                        CopyToArray(result, i - (int)num6, result, i, 3);
                        i += 3;
                        num++;
                    }
                    else if ((num5 & 2U) == 0U)
                    {
                        uint num6 = (num5 & 65535U) >> 2;
                        CopyToArray(result, i - (int)num6, result, i, 3);
                        i += 3;
                        num += 2;
                    }
                    else if ((num5 & 1U) == 0U)
                    {
                        uint num6 = (num5 & 65535U) >> 6;
                        uint num7 = (num5 >> 2 & 15U) + 3U;
                        CopyToArray(result, i - (int)num6, result, i, (int)num7);
                        i += (int)num7;
                        num += 2;
                    }
                    else if ((num5 & 4U) == 0U)
                    {
                        uint num6 = (num5 & 16777215U) >> 8;
                        uint num7 = (num5 >> 3 & 31U) + 3U;
                        CopyToArray(result, i - (int)num6, result, i, (int)num7);
                        i += (int)num7;
                        num += 3;
                    }
                    else if ((num5 & 8U) == 0U)
                    {
                        uint num6 = num5 >> 15;
                        uint num7;
                        if (num6 != 0U)
                        {
                            num7 = (num5 >> 4 & 2047U) + 3U;
                            num += 4;
                        }
                        else
                        {
                            num7 = BitShift(src, num + 4, 4);
                            num6 = BitShift(src, num + 8, 4);
                            num += 12;
                        }
                        CopyToArray(result, i - (int)num6, result, i, (int)num7);
                        i += (int)num7;
                    }
                    else
                    {
                        byte b = (byte)(num5 >> 16);
                        uint num7 = num5 >> 4 & 4095U;
                        if (num7 != 0U)
                        {
                            num += 3;
                        }
                        else
                        {
                            num7 = BitShift(src, num + 3, 4);
                            num += 7;
                        }
                        int num8 = 0;
                        while ((long)num8 < (long)((ulong)num7))
                        {
                            result[i++] = b;
                            num8++;
                        }
                    }
                }
                else
                {
                    CopyToArray(src, num, result, i, 4);
                    int num9 = (int)(num4 & 15U);
                    int num10 = 0;
                    if (num9 % 2 == 0)
                    {
                        num9 /= 2;
                        if (num9 % 2 > 0)
                        {
                            num10 = 1;
                        }
                        else
                        {
                            num9 /= 2;
                            num10 = ((num9 % 2 > 0) ? 2 : ((num9 == 0) ? 4 : 3));
                        }
                    }
                    i += num10;
                    num += num10;
                    num4 >>= num10;
                    if (i >= num3)
                    {
                        break;
                    }
                }
            }
            while (i < num2)
            {
                if (num4 == 1U)
                {
                    num += 4;
                    num4 = 2147483648U;
                }
                if (1 > resultIndex + 260 - i)
                {
                    return result;
                }
                result[i++] = src[num++];
                num4 >>= 1;
            }
            return result;
        }
    }
}
