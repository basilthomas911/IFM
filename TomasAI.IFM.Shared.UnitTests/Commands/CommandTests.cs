using System;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventSourcing
{
    public class CommandTests
    {
        [Fact]
        public void LoadCommandsWithICommandInterface()
        {
            // load assembly...
            Assembly assembly = SharedDomainAssembly.Current;
            var assemblyName = assembly.GetName().Name;
            try
            {
    
                // get all types...
                Type[] types = assembly.GetTypes();

                foreach (var type in types)
                {
                    // get all interfaces of the type
                    var interfaces = type.GetInterfaces();

                    // check if the type implements a specific interface
                    if (interfaces.Any(i => i.Name == "ICommand" && !i.IsAbstract))
                    {
                        Debug.WriteLine($"Domain Command: {type.Name}");
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                Debug.WriteLine($"Unable to load types from assembly {assemblyName}. Exception: {ex}");
                foreach (var loaderException in ex.LoaderExceptions)
                {
                    Console.WriteLine($"Loader Exception: {loaderException}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to load assembly {assemblyName}. Exception: {ex}");
            }
            Debug.WriteLine($"Done!!!");
        }

    }
}
