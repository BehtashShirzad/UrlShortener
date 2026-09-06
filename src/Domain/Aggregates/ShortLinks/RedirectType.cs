using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Aggregates.ShortLinks
{
    public enum RedirectType
    {
        Temporary = 302,
        Permanent = 301
    }
}
