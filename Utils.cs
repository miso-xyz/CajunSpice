using System; 
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet.Emit;
using dnlib.DotNet;
using System.Reflection;

namespace CajunSpice
{
    class Utils
    {
        public static int GetLocalIndex(Instruction inst)
        {
            return inst.Operand == null ? int.Parse(inst.OpCode.ToString().Split('.')[1]) : inst.Operand is Local ? ((Local)inst.Operand).Index : Convert.ToInt32(inst.Operand);
        }

        /*public static byte[] FilterByteFromArray(byte[] array, byte value) - forgot unicode exists lmao
        {
            List<byte> result = array.ToList();
            for (int x = 0; x < result.Count; x++)
            {
                if (result[x] == value) { result.RemoveAt(x); }
            }
            return result.ToArray();
        }*/

        public static string SerializeMethod(MethodDef method)
        {
            string result = "RET:" + method.MethodSig.RetType.TypeName + "-";
            foreach (TypeSig typeSig in method.MethodSig.Params)
            {
                result += typeSig.TypeName + ",";
            }
            result += "-" + (method.HasBody ? (method.Body.Instructions.Count * method.Body.Instructions[method.Body.Instructions.Count - 2].Offset * method.Body.Instructions[method.Body.Instructions.Count - 2].OpCode.Value) : 0) + "_" ;
            return result;
        }

        public static TypeDef GetRootTypeDef(TypeDef type)
        {
            while (type.DeclaringType != null) { type = type.DeclaringType; }
            return type;
        }

        public static void MoveMethod(MethodDef method, TypeDef destType)
        {
            method.DeclaringType = null;
            destType.Methods.Add(method);
            //method.DeclaringType.Methods.Remove(method);
        }

        public static bool IsPropertyMethod(TypeDef declType, MethodDef method)
        {
            foreach (PropertyDef prop in declType.Properties) { if (prop.GetMethods.Contains(method)) { return true; } }
            return false;
        }

        public static void SetEntrypoint(ModuleDefMD asm, MethodDef method) { asm.ManagedEntryPoint = method as IManagedEntryPoint; }

        public static TypeDef[] GetTypeDefAttributes(ModuleDefMD asm)
        {
            List<TypeDef> result = new List<TypeDef>();
            foreach (CustomAttribute ca in asm.Assembly.CustomAttributes)
            {
                if (ca.AttributeType.IsTypeDef) { result.Add(ca.AttributeType as TypeDef); }
            }
            return result.ToArray();
        }

        public static bool CompareTypeRef(Instruction srcInst, Type matchType)
        {
            return
                ((TypeRef)srcInst.Operand).Namespace + "." + ((TypeRef)srcInst.Operand).Name ==
                matchType.Namespace + "." + matchType.Name;
        }

        public static bool CompareMemberRef(Instruction srcInst, Type matchType, string methodName, Type[] methodArgs = null)
        {
            string instNamespace = ((MemberRef)srcInst.Operand).DeclaringType.FullName + "." + ((MemberRef)srcInst.Operand).Name,
                   matchClassNamespace = matchType.FullName,
                   matchMethodName = "";
            if (methodArgs == null) { matchMethodName = matchType.GetMethod(methodName).Name; }
            else { matchMethodName = matchType.GetMethod(methodName, methodArgs).Name; }
            return instNamespace == matchClassNamespace + "." + matchMethodName;
        }

        public static bool CompareMethodSpec(Instruction srcInst, Type matchType, string methodName, Type[] methodArgs = null)
        {
            string instNamespace = ((MethodSpec)srcInst.Operand).DeclaringType.FullName + "." + ((MethodSpec)srcInst.Operand).Name,
                   matchClassNamespace = matchType.FullName,
                   matchMethodName = "";
            if (methodArgs == null) { matchMethodName = matchType.GetMethod(methodName).Name; }
            else { matchMethodName = matchType.GetMethod(methodName, methodArgs).Name; }
            return instNamespace == matchClassNamespace + "." + matchMethodName;
        }

        public static TypeDef[] GetAllNestedTypes(TypeDef type)
        {
            List<TypeDef> result = new List<TypeDef>();
            foreach (TypeDef nsType in type.NestedTypes)
            {
                if (nsType.HasNestedTypes) { result.AddRange(GetAllNestedTypes(nsType)); }
                result.Add(nsType);
            }
            return result.ToArray();
        }
        public static TypeDef[] GetAllTypes(ModuleDefMD asm)
        {
            List<TypeDef> result = new List<TypeDef>();
            foreach (TypeDef type in asm.Types)
            {
                if (type.HasNestedTypes) { result.AddRange(GetAllNestedTypes(type)); }
                result.Add(type);
            }
            return result.ToArray();
        }
    }
}
