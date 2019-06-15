using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace OnWebSocketMessageQueued
{
    public interface IConfigurationService
    {
        IConfiguration GetConfiguration();
    }
}
