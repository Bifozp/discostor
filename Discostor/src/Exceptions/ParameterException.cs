using System;
using System.Collections.Generic;

namespace Impostor.Plugins.Discostor.Exceptions
{
    internal class RequiredPropertyException : Exception
    {
        internal List<string> MissingProperties;

        internal RequiredPropertyException(string s)
            : base(s){}
    }
}
