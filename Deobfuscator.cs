using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using dnlib.DotNet.Emit;
using System.IO;
using dnlib.PE;

namespace CajunSpice
{
    class Deobfuscator
    {
        public IList<ProxyMethodTree> ProxyMethods;

        public Deobfuscator(string path)
        {
            asmPath = Path.GetFullPath(path);
            rawAsm = File.ReadAllBytes(path);
            asm = ModuleDefMD.Load(rawAsm);

            NopCleaning.CleanASM(asm);
        }

        public string[] GetNames()
        {
            List<string> result = new List<string>();
            foreach (MethodDef method in asm.EntryPoint.DeclaringType.Methods) { result.Add(method.Name); }
            return result.ToArray();
        }

        public void Fix()
        {
            StringFixer.Fix(asm);
            Console.WriteLine();
            FixProxy();
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Removing junk...");
            NopCleaning.RemoveUnusedObjects(asm);
            Attributes.Fix(asm);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Junk cleaned!");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Saving file...");
            Save();
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("File saved at '" + Environment.CurrentDirectory + "\\" + Path.GetFileNameWithoutExtension(asmPath) + "-CajunSpice" + Path.GetExtension(asmPath) + "!");
            Console.ReadKey();
        }

        public void Save()
        {
            ModuleWriterOptions moduleWriterOptions = new ModuleWriterOptions(asm);
            moduleWriterOptions.MetadataOptions.Flags |= MetadataFlags.KeepOldMaxStack;
            moduleWriterOptions.Logger = DummyLogger.NoThrowInstance;
            asm.Write(Path.GetFileNameWithoutExtension(asmPath) + "-CajunSpice" + Path.GetExtension(asmPath), moduleWriterOptions);
        }

        public void FixProxy()
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Searching for Proxy Methods...");
            GetNames();
            ProxyMethodTree.ResolveProxies(asm, out ProxyMethods);

            if (ProxyMethods.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No Proxy Methods Found!");
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Found " + ProxyMethods.Count + " Proxy Method(s)!\n");
            foreach (TypeDef type in Utils.GetAllTypes(asm))
            {
                Renaming.RenameTypesFromAttributes(asm);
                foreach (MethodDef method in type.Methods)
                {
                    if (!method.HasBody) { continue; }
                    for (int x = 0; x < method.Body.Instructions.Count; x++)
                    {
                        Instruction inst = method.Body.Instructions[x];
                        switch (inst.OpCode.Code)
                        {
                            case Code.Box:
                                if (method.IsConstructor && method.Body.Instructions[x + 1].OpCode == OpCodes.Stsfld)
                                {
                                    FieldDef field = (FieldDef)method.Body.Instructions[x + 1].Operand;
                                    if (inst.Operand is TypeRef)
                                    {
                                        field.FieldType = ((TypeRef)inst.Operand).ToTypeSig();
                                        method.Body.Instructions.RemoveAt(x);
                                        x--;
                                    }
                                }
                                break;
                            case Code.Callvirt:
                            case Code.Call:
                                if (inst.Operand is MethodDef)
                                {
                                    MethodDef instMethod = (MethodDef)inst.Operand, prEnd = null;
                                    ProxyMethodTree prMethod = ProxyMethodTree.IsProxyMethod(instMethod) ? ProxyMethodTree.ResolveProxy(instMethod) : null;
                                    if (prMethod == null) { break; }
                                    string ogCall = "";
                                    if (prMethod.OriginalInstruction.Operand is MethodDef) { prEnd = (MethodDef)prMethod.OriginalObject; }
                                    switch (prMethod.OriginalInstruction.Operand.GetType().Name)
                                    {
                                        case "MemberRef":
                                            ogCall = prEnd.DeclaringType.Name + "." + prEnd.Name; break;
                                        case "MethodDef":
                                            ogCall = ((MethodDef)prMethod.OriginalInstruction.Operand).DeclaringType.Name + "." + ((MethodDef)prMethod.OriginalInstruction.Operand).Name; break;
                                        case "FieldDef":
                                            ogCall = ((FieldDef)prMethod.OriginalInstruction.Operand).DeclaringType.Name + "." + ((FieldDef)prMethod.OriginalInstruction.Operand).Name; break;
                                    }
                                    Console.ForegroundColor = ConsoleColor.Cyan;
                                    Console.WriteLine("[Proxy]: Recovered '" + ogCall + "' from " + instMethod.Name + "::" + inst.Offset.ToString("X4"));
                                    if (prEnd != null)
                                    {
                                        if (asm.EntryPoint == instMethod) { Utils.SetEntrypoint(asm, prEnd); }
                                        if (instMethod.HasCustomAttributes) { foreach (CustomAttribute ca in instMethod.CustomAttributes) { prEnd.CustomAttributes.Add(ca); } }
                                        prEnd.Name = prMethod.OriginalName;
                                    }
                                    inst.OpCode = prMethod.OriginalInstruction.OpCode;
                                    inst.Operand = prMethod.OriginalInstruction.Operand;
                                }
                                break;
                        }
                    }
                }
            }
            TypeDef rootType = Utils.GetRootTypeDef(asm.EntryPoint.DeclaringType);
            int methodCount = rootType.Methods.Count;
            foreach (ProxyMethodTree prMethod in ProxyMethods)
            {
                if (prMethod.OriginalMethod == null) { continue; }
                if (prMethod.OriginalName != null)
                {
                    prMethod.OriginalMethod.Name = prMethod.OriginalName;
                }
            }
            ProxyMethodTree.RemoveProxyMethods(asm, ProxyMethods);
            TypeDef entrypointType = asm.EntryPoint.DeclaringType;
            while (!entrypointType.HasMethods)
            {
                for (int x = 0; x < entrypointType.Methods.Count; x++)
                {
                    MethodDef method = entrypointType.Methods[x];
                    if (method.IsConstructor) { entrypointType.Methods.Remove(method); }
                    Utils.MoveMethod(method, Utils.GetRootTypeDef(entrypointType));
                    if (method == asm.EntryPoint) { Utils.SetEntrypoint(asm, rootType.Methods[rootType.Methods.Count - 1]);  }
                }
            }
        }

        ModuleDefMD asm;
        string asmPath;
        byte[] rawAsm;
    }
}