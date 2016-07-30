using System;
using System.Collections.Generic;
using System.Text;


public enum ImportState
{
    Waiting = 1,
    Running = 2,
    Disabled = 3,
    Scheduled = 4
}

namespace Athena.Core
{
    public interface IImport
    {

        bool RunImport(string Server, string Database, string Username, string Password);

        ImportState GetState();

        List<string> GetErrors();

    }
}
