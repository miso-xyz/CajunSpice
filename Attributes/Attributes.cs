using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace CajunSpice
{
    class Attributes
    {
        public static void Fix(ModuleDefMD asm)
        {
            TypeDef[] types = Utils.GetTypeDefAttributes(asm);
            foreach (TypeDef type in types)
            {
                for (int x = 0; x < asm.Assembly.CustomAttributes.Count; x++)
                {
                    CustomAttribute ca = asm.Assembly.CustomAttributes[x];
                    if (ca.AttributeType.IsTypeDef && type.Name == "Evaluation" && type == ca.AttributeType)
                    {
                        asm.Assembly.CustomAttributes.Remove(ca);
                        asm.Types.Remove(type);
                        x--;
                    }
                }
            }
        }
    }
}
