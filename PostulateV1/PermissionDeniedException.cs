﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postulate
{
    public class PermissionDeniedException : Exception
    {
        public PermissionDeniedException(string message) : base(message)
        {
        }
    }
}
