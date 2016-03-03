public class Plugin : IPlugin
{
      ...
      
        /// <summary>
        /// The methods exposes the RegisteredEvents as a collection of tuples
        /// containing:
        /// - The full assembly name of the class containing the RegisteredEvents
        /// - The Pipeline Stage
        /// - The Event Operation
        /// - Logical Entity Name (or empty for all)
        /// This will allow to instantiate each plug-in and iterate through the 
        /// PluginProcessingSteps in order to sync the code repository with 
        /// MS CRM without have to use any extra layer to perform this operation
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Tuple<string, int, string, string>> PluginProcessingSteps()
  {
    var className = this.ChildClassName;
    foreach (var events in this.RegisteredEvents)
    {
      yield return new Tuple<string, int, string, string>
          (className, events.Item1, events.Item2, events.Item3);
    }
  }
        
        ...
        
    }