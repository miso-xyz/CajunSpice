using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Runtime.CompilerServices;

namespace CajunSpice
{
    public class StringMethod
    {
        public StringMethod(MethodDef method, FieldDef src, FieldDef encoding, int pos, int length)
        {
            srcMethod = method;
            srcBytes = src;
            encField = encoding;
            index = pos;
            count = length;
        }

        public static bool Contains(MethodDef method, StringMethod[] stringMethods, out StringMethod container)
        {
            container = null;
            foreach (StringMethod stringMethod in stringMethods)
            {
                if (stringMethod.srcMethod == method) { container = stringMethod; return true; }
            }
            return false;
        }

        public MethodDef srcMethod;
        public FieldDef srcBytes, encField;
        public int index, count;
    }
}