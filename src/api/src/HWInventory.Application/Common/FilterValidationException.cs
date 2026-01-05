using System;
using System.Collections.Generic;
using System.Linq;

namespace HWInventory.Application.Common;

public class FilterValidationException : Exception
{
    public FilterValidationException(IEnumerable<string> errors)
        : base("Filter validation failed")
    {
        Errors = errors?.ToArray() ?? Array.Empty<string>();
    }

    public IReadOnlyList<string> Errors { get; }
}
