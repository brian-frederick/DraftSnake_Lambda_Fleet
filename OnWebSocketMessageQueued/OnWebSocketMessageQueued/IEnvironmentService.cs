using System;
using System.Collections.Generic;
using System.Text;

namespace OnWebSocketMessageQueued
{
    public interface IEnvironmentService
    {
        string EnvironmentName { get; set; }
    }
}
