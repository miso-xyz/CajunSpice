using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace CajunSpice
{
    public enum ProxyType { FieldProxy, CallProxy }

    class ProxyMethodTree
    {
        public ProxyMethodTree(MethodDef method)
        {
            ProxyMethods = new List<MethodDef>();
            ProxyInstructions = new List<Instruction>();

            _srcMethod = method;
        }

        public IList<MethodDef> ProxyMethods { get; set; }
        public IList<Instruction> ProxyInstructions { get; set; }

        private MethodDef _srcMethod;
        public MethodDef SourceMethod { get { return _srcMethod; } }

        public MethodDef OriginalMethod { get {  try { return (MethodDef)OriginalObject; } catch { } return null; } }
        public object OriginalObject { get { return ProxyInstructions[ProxyInstructions.Count - 1].Operand; } }
        public string OriginalName
        {
            get
            {
                foreach (MethodDef prMethod in ProxyMethods)
                {
                    if (prMethod.DeclaringType == Utils.GetRootTypeDef(prMethod.DeclaringType))
                    {
                        return prMethod.Name;
                    }
                }
                return null;
            }
        }

        public Instruction OriginalInstruction { get { return ProxyInstructions[ProxyInstructions.Count - 1]; } }

        public ProxyType ProxyType
        {
            get
            {
                switch (OriginalInstruction.OpCode.Code)
                {
                    case Code.Call:
                    case Code.Callvirt:
                        return ProxyType.CallProxy;
                    case Code.Ldsfld:
                    case Code.Ldsflda:
                        return ProxyType.FieldProxy;
                    default: throw new Exception("Unknown ProxyType! (OpCode: '" + OriginalInstruction.OpCode.ToString() + "')");
                }
            }
        }

        public static ProxyMethodTree ResolveProxy(MethodDef method)
        {
            ProxyMethodTree result = new ProxyMethodTree(method);

            result.ProxyMethods.Add(method);

            //if (!IsProxyMethod(method)) { throw new Exception("Not a proxy method!"); }

            MethodDef newMethodPr = null, propMethod = null;
            Instruction[] insts = method.Body.Instructions.ToArray();
            for (int x = 0; x < insts.Length; x++)
            {
                bool exitLoop = false, newMethod = false;
                Instruction inst = insts[x];
                switch (inst.OpCode.Code)
                {
                    case Code.Ldfld:
                    case Code.Ldsfld:
                    case Code.Ldsflda:
                        result.ProxyMethods.Add(method);
                        result.ProxyInstructions.Add(inst);
                        exitLoop = true;
                        break;
                    case Code.Ldftn:
                    case Code.Callvirt:
                    case Code.Call:
                        result.ProxyInstructions.Add(inst);
                        result.ProxyMethods.Add(method);
                        if (inst.Operand is MethodDef && IsProxyMethod((MethodDef)inst.Operand))
                        {
                            if (Utils.IsPropertyMethod(method.DeclaringType, method))
                            {
                                propMethod = method;
                            }
                            newMethod = true;
                            newMethodPr = (MethodDef)inst.Operand;
                            result.ProxyMethods.Add(newMethodPr);
                        }
                        else
                        {
                            exitLoop = true;
                        }
                        break;
                }
                if (exitLoop) { break; }
                if (newMethod) { method = newMethodPr; insts = method.Body.Instructions.ToArray(); x = 0; }
            }
            return result;
        }
        public static void ResolveProxies(ModuleDefMD asm, out IList<ProxyMethodTree> prTreeList)
        {
            prTreeList = new List<ProxyMethodTree>();
            foreach (TypeDef type in Utils.GetAllTypes(asm))
            {
                foreach (MethodDef method in type.Methods)
                {
                    if (method.IsConstructor) { continue; }
                    if (IsProxyMethod(method))
                    {
                        if (Contains(prTreeList, method) == false)
                        {
                            prTreeList.Add(ResolveProxy(method));
                        }
                    }
                }
            }
        }

        public static bool Contains(IList<ProxyMethodTree> prMethodTreeList, MethodDef method)
        {
            ProxyMethodTree result = null;
            return Contains(prMethodTreeList, method, out result);
        }
        public static bool Contains(IList<ProxyMethodTree> prMethodTreeList, MethodDef method, out ProxyMethodTree proxyMethod)
        {
            proxyMethod = null;
            foreach (ProxyMethodTree prMethodTree in prMethodTreeList)
            {
                if (prMethodTree.Contains(method)) { proxyMethod = prMethodTree; return true; }
            }
            return false;
        }
        public bool Contains(MethodDef method)
        {
            foreach (MethodDef prMethod in ProxyMethods)
            {
                if (prMethod == method) { return true; }
            }
            return false;
        }

        public static void RemoveProxyMethods(ModuleDefMD asm, IList<ProxyMethodTree> proxyMethodTreeList)
        {
            foreach (TypeDef type in Utils.GetAllTypes(asm))
            {
                foreach (ProxyMethodTree prMethodTree in proxyMethodTreeList)
                {
                    foreach (MethodDef prMethod in prMethodTree.ProxyMethods)
                    {
                        if (type.Methods.Contains(prMethod))
                        {
                            if (Utils.IsPropertyMethod(type, prMethod))
                            {
                                if (prMethodTree.OriginalMethod == null)
                                {
                                    prMethod.Body = prMethodTree.ProxyMethods[prMethodTree.ProxyMethods.Count-1].Body;
                                }
                                else
                                {
                                    prMethod.Body = prMethodTree.OriginalMethod.Body;
                                }
                            }
                            else
                            {
                                type.Methods.Remove(prMethod);
                            }
                        }
                    }
                }
            }
        }

        public static bool IsProxyMethod(MethodDef method)
        {
            if (method.IsSpecialName) { return false; }
            for (int x = method.Parameters.Count; x < method.Body.Instructions.Count; x++)
            {
                Instruction inst = method.Body.Instructions[x];
                if (inst.IsLdarg()) { continue; }
                switch (inst.OpCode.Code)
                {
                    case Code.Ldfld:
                    case Code.Ldsfld:
                    case Code.Ldsflda:
                    case Code.Call:
                    case Code.Ldftn:
                    case Code.Callvirt:
                        break;
                    case Code.Ret:
                        return true;
                    default:
                        return false;
                }
            }
            return false;
            //return (method.Body.Instructions[method.Parameters.Count+1].OpCode == OpCodes.Ret);
        }

        /*private MethodDef[] GetNonProxyMethods(MethodDef[] proxyMethodsList)
        {
            List<MethodDef> result = new List<MethodDef>();
            foreach (TypeDef type in Utils.GetAllTypes(asm))
            {
                foreach (MethodDef method in type.Methods)
                {
                    if (!proxyMethodsList.Contains(method))
                    {
                        result.Add(method);
                    }
                }
            }
            return result.ToArray();
        }*/
        /*private static MethodDef[] GetProxyMethods(ModuleDefMD asm, out object[] ogCalls)
        {
            List<object> ogCallsList = new List<object>();
            List<MethodDef> prMethodList = new List<MethodDef>();
            foreach (TypeDef type in Utils.GetAllTypes(asm))
            {
                foreach (MethodDef method in type.Methods)
                {
                    if (!method.HasBody) { continue; }
                    object ogCall = null;
                    MethodDef prMethod = null;
                    ResolveProxyMethod(method, out prMethod, out ogCall);
                    if (ogCall != null)
                    {
                        ogCallsList.Add(ogCall);
                        prMethodList.Add(prMethod);
                    }
                }
            }
            ogCalls = ogCallsList.ToArray();
            return prMethodList.ToArray();
        }*/
    }
}
