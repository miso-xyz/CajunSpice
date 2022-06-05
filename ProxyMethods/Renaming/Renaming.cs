using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using System.IO;

namespace CajunSpice
{
    class Renaming
    {
        public static void RenameTypesFromAttributes(ModuleDefMD asm)
        {
            asm.EntryPoint.DeclaringType.Name = "Program";
            asm.EntryPoint.DeclaringType.Namespace = Path.GetFileNameWithoutExtension(asm.Name);
            foreach (TypeDef type in Utils.GetAllTypes(asm))
            {
                if (type.BaseType == null && type.IsGlobalModuleType) { continue; }
                switch (type.BaseType.TypeName)
                {
                    case "ConsoleApplicationBase":
                    case "WindowsFormsApplicationBase":
                    case "ApplicationBase":
                        type.Namespace = Path.GetFileNameWithoutExtension(type.Module.Name) + ".My";
                        type.Name = "MyApplication";
                        break;
                    case "Computer":
                        type.Namespace = Path.GetFileNameWithoutExtension(type.Module.Name) + ".My";
                        type.Name = "MyComputer";
                        break;
                }
                foreach (CustomAttribute ca in type.CustomAttributes)
                {
                    foreach (CAArgument caa in ca.ConstructorArguments)
                    {
                        switch (ca.AttributeType.TypeName)
                        {
                            case "GeneratedCodeAttribute":
                                if (caa.Value.ToString() == "MyTemplate" && type.HasProperties)
                                {
                                    type.Namespace = Path.GetFileNameWithoutExtension(type.Module.Name) + ".My";
                                    type.Name = "MyProject";
                                    if (type.HasNestedTypes)
                                    {
                                        foreach (TypeDef nsType in Utils.GetAllNestedTypes(type))
                                        {
                                            if (nsType.HasGenericParameters)
                                            {
                                                nsType.Name = "ThreadSafeObjectProvider";
                                                break;
                                            }
                                        }
                                    }
                                }
                                break;
                            case "MyGroupCollectionAttribute":
                                if (caa.Value.ToString() == "System.Web.Services.Protocols.SoapHttpClientProtocol")
                                {
                                    type.Name = "MyWebServices";
                                }
                                break;
                        }
                    }
                }
            }
        }
    }
}
