namespace DG.Some.Namespace
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.ServiceModel;
    using System.Linq.Expressions;
    using Microsoft.Xrm.Sdk;

    // StepConfig           : className, ExecutionStage, EventOperation, LogicalName
    // ExtendedStepConfig   : Deployment, ExecutionMode, Name, ExecutionOrder, FilteredAttributes
    // ImageTuple           : Name, EntityAlias, ImageType, Attributes
    using StepConfig = System.Tuple<string, int, string, string>;
    using ExtendedStepConfig = System.Tuple<int, int, string, int, string>;
    using ImageTuple = System.Tuple<string, string, int, string>;
    

    /// <summary>
    /// Base class for all Plugins.
    /// </summary>    
    public class Plugin : IPlugin
    {
        protected class LocalPluginContext
        {
            internal IServiceProvider ServiceProvider
            {
                get;

                private set;
            }

            internal IOrganizationService OrganizationService
            {
                get;

                private set;
            }

            // Delegate A/S added:
            internal IOrganizationService OrganizationAdminService
            {
                get;

                private set;
            }

            internal IPluginExecutionContext PluginExecutionContext
            {
                get;

                private set;
            }

            internal ITracingService TracingService
            {
                get;

                private set;
            }

            private LocalPluginContext()
            {
            }

            internal LocalPluginContext(IServiceProvider serviceProvider)
            {
                if (serviceProvider == null)
                {
                    throw new ArgumentNullException("serviceProvider");
                }

                // Obtain the execution context service from the service provider.
                this.PluginExecutionContext = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

                // Obtain the tracing service from the service provider.
                this.TracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

                // Obtain the Organization Service factory service from the service provider
                IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

                // Use the factory to generate the Organization Service.
                this.OrganizationService = factory.CreateOrganizationService(this.PluginExecutionContext.UserId);

                // Delegate A/S added: Use the factory to generate the Organization Admin Service.
                this.OrganizationAdminService = factory.CreateOrganizationService(null);
            }

            internal void Trace(string message)
            {
                if (string.IsNullOrWhiteSpace(message) || this.TracingService == null)
                {
                    return;
                }

                if (this.PluginExecutionContext == null)
                {
                    this.TracingService.Trace(message);
                }
                else
                {
                    this.TracingService.Trace(
                        "{0}, Correlation Id: {1}, Initiating User: {2}",
                        message,
                        this.PluginExecutionContext.CorrelationId,
                        this.PluginExecutionContext.InitiatingUserId);
                }
            }
        }

        private Collection<Tuple<int, string, string, Action<LocalPluginContext>>> registeredEvents;

        /// <summary>
        /// Gets the List of events that the plug-in should fire for. Each List
        /// Item is a <see cref="System.Tuple"/> containing the Pipeline Stage, Message and (optionally) the Primary Entity. 
        /// In addition, the fourth parameter provide the delegate to invoke on a matching registration.
        /// </summary>
        protected Collection<Tuple<int, string, string, Action<LocalPluginContext>>> RegisteredEvents
        {
            get
            {
                if (this.registeredEvents == null)
                {
                    this.registeredEvents = new Collection<Tuple<int, string, string, Action<LocalPluginContext>>>();
                }

                return this.registeredEvents;
            }
        }

        /// <summary>
        /// Gets or sets the name of the child class.
        /// </summary>
        /// <value>The name of the child class.</value>
        protected string ChildClassName
        {
            get;

            private set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="childClassName">The <see cref="" cred="Type"/> of the derived class.</param>
        internal Plugin(Type childClassName)
        {
            this.ChildClassName = childClassName.ToString();
        }


        /// <summary>
        /// Executes the plug-in.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <remarks>
        /// For improved performance, Microsoft Dynamics CRM caches plug-in instances. 
        /// The plug-in's Execute method should be written to be stateless as the constructor 
        /// is not called for every invocation of the plug-in. Also, multiple system threads 
        /// could execute the plug-in at the same time. All per invocation state information 
        /// is stored in the context. This means that you should not use global variables in plug-ins.
        /// </remarks>
        public void Execute(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException("serviceProvider");
            }

            // Construct the Local plug-in context.
            LocalPluginContext localcontext = new LocalPluginContext(serviceProvider);

            localcontext.Trace(string.Format(CultureInfo.InvariantCulture, "Entered {0}.Execute()", this.ChildClassName));

            try
            {
                // Iterate over all of the expected registered events to ensure that the plugin
                // has been invoked by an expected event
                // For any given plug-in event at an instance in time, we would expect at most 1 result to match.
                Action<LocalPluginContext> entityAction =
                    (from a in this.RegisteredEvents
                     where (
                     a.Item1 == localcontext.PluginExecutionContext.Stage &&
                     a.Item2 == localcontext.PluginExecutionContext.MessageName &&
                     (string.IsNullOrWhiteSpace(a.Item3) ? true : a.Item3 == localcontext.PluginExecutionContext.PrimaryEntityName)
                     )
                     select a.Item4).FirstOrDefault();

                if (entityAction != null)
                {
                    localcontext.Trace(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} is firing for Entity: {1}, Message: {2}",
                        this.ChildClassName,
                        localcontext.PluginExecutionContext.PrimaryEntityName,
                        localcontext.PluginExecutionContext.MessageName));

                    entityAction.Invoke(localcontext);

                    // now exit - if the derived plug-in has incorrectly registered overlapping event registrations,
                    // guard against multiple executions.
                    return;
                }
            }
            catch (FaultException<OrganizationServiceFault> e)
            {
                localcontext.Trace(string.Format(CultureInfo.InvariantCulture, "Exception: {0}", e.ToString()));

                // Handle the exception.
                throw;
            }
            finally
            {
                localcontext.Trace(string.Format(CultureInfo.InvariantCulture, "Exiting {0}.Execute()", this.ChildClassName));
            }
        }


        // Delegate A/S added:
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

        #region PluginStepConfig retrieval
        /// <summary>
        /// Made by Delegate A/S
        /// Get the plugin step configurations.
        /// </summary>
        /// <returns>List of steps</returns>
        public IEnumerable<Tuple<StepConfig, ExtendedStepConfig, IEnumerable<ImageTuple>>> PluginProcessingStepConfigs()
        {
            var className = this.ChildClassName;
            foreach (var config in this.PluginStepConfigs)
            {
                yield return
                    new Tuple<StepConfig, ExtendedStepConfig, IEnumerable<ImageTuple>>(
                        new StepConfig(className, config.ExecutionStage, config.EventOperation, config.LogicalName),
                        new ExtendedStepConfig(config.Deployment, config.ExecutionMode, config.Name, config.ExecutionOrder, config.FilteredAttributes),
                        config.GetImages());
            }
        }


        protected PluginStepConfig<T> RegisterPluginStep<T>(
            EventOperation eventOperation, ExecutionStage executionStage, Action<LocalPluginContext> action)
            where T : Entity
        {
            PluginStepConfig<T> stepConfig = new PluginStepConfig<T>(eventOperation, executionStage);
            this.PluginStepConfigs.Add((IPluginStepConfig)stepConfig);

            this.RegisteredEvents.Add(
                new Tuple<int, string, string, Action<LocalPluginContext>>(
                    stepConfig.ExecutionStage,
                    stepConfig.EventOperation,
                    stepConfig.LogicalName,
                    new Action<LocalPluginContext>(action)));

            return stepConfig;
        }


        private Collection<IPluginStepConfig> pluginConfigs;
        private Collection<IPluginStepConfig> PluginStepConfigs
        {
            get
            {
                if (this.pluginConfigs == null)
                {
                    this.pluginConfigs = new Collection<IPluginStepConfig>();
                }
                return this.pluginConfigs;
            }
        }
        #endregion

    }

    #region PluginStepConfig made by Delegate A/S
    interface IPluginStepConfig
    {
        string LogicalName { get; }
        string EventOperation { get; }
        int ExecutionStage { get; }

        string Name { get; }
        int Deployment { get; }
        int ExecutionMode { get; }
        int ExecutionOrder { get; }
        string FilteredAttributes { get; }
        IEnumerable<ImageTuple> GetImages();
    }

    /// <summary>
    /// Made by Delegate A/S
    /// Class to encapsulate the various configurations that can be made 
    /// to a plugin step.
    /// </summary>
    public class PluginStepConfig<T> : IPluginStepConfig where T : Entity
    {
        public string LogicalName { get; private set; }
        public string EventOperation { get; private set; }
        public int ExecutionStage { get; private set; }

        public string Name { get; private set; }
        public int Deployment { get; private set; }
        public int ExecutionMode { get; private set; }
        public int ExecutionOrder { get; private set; }

        private Collection<PluginStepImage> Images = new Collection<PluginStepImage>();

        private Collection<string> FilteredAttributesCollection = new Collection<string>();
        public string FilteredAttributes
        {
            get
            {
                if (this.FilteredAttributesCollection.Count == 0) return null;
                return string.Join(",", this.FilteredAttributesCollection).ToLower();
            }
        }


        public PluginStepConfig(EventOperation eventOperation, ExecutionStage executionStage)
        {
            this.LogicalName = Activator.CreateInstance<T>().LogicalName;
            this.EventOperation = eventOperation.ToString();
            this.ExecutionStage = (int)executionStage;
            this.Deployment = (int)Plugins.Deployment.ServerOnly;
            this.ExecutionMode = (int)Plugins.ExecutionMode.Synchronous;
            this.ExecutionOrder = 1;
        }

        private PluginStepConfig<T> AddFilteredAttribute(Expression<Func<T, object>> lambda)
        {
            this.FilteredAttributesCollection.Add(GetMemberName(lambda));
            return this;
        }

        public PluginStepConfig<T> AddFilteredAttributes(params Expression<Func<T, object>>[] lambdas)
        {
            foreach (var lambda in lambdas) this.AddFilteredAttribute(lambda);
            return this;
        }

        public PluginStepConfig<T> SetDeployment(Deployment deployment)
        {
            this.Deployment = (int)deployment;
            return this;
        }

        public PluginStepConfig<T> SetExecutionMode(ExecutionMode executionMode)
        {
            this.ExecutionMode = (int)executionMode;
            return this;
        }

        public PluginStepConfig<T> SetName(string name)
        {
            this.Name = name;
            return this;
        }

        public PluginStepConfig<T> SetExecutionOrder(int executionOrder)
        {
            this.ExecutionOrder = executionOrder;
            return this;
        }





        public PluginStepConfig<T> AddImage(ImageType imageType)
        {
            return this.AddImage(imageType, null);
        }

        public PluginStepConfig<T> AddImage(ImageType imageType, params Expression<Func<T, object>>[] attributes)
        {
            return this.AddImage(imageType.ToString(), imageType.ToString(), imageType, attributes);
        }

        public PluginStepConfig<T> AddImage(string name, string entityAlias, ImageType imageType)
        {
            return this.AddImage(name, entityAlias, imageType, null);
        }

        public PluginStepConfig<T> AddImage(string name, string entityAlias, ImageType imageType, params Expression<Func<T, object>>[] attributes)
        {
            this.Images.Add(new PluginStepImage(name, entityAlias, imageType, attributes));
            return this;
        }

        public IEnumerable<ImageTuple> GetImages()
        {
            foreach (var image in this.Images)
            {
                yield return new ImageTuple(image.Name, image.EntityAlias, image.ImageType, image.Attributes);
            }
        }



        /// <summary>
        /// Container for information about images attached to steps
        /// </summary>
        public class PluginStepImage
        {
            public string Name { get; private set; }
            public string EntityAlias { get; private set; }
            public int ImageType { get; private set; }
            public string Attributes { get; private set; }

            public PluginStepImage(string name, string entityAlias, ImageType imageType, Expression<Func<T, object>>[] attributes)
            {
                this.Name = name;
                this.EntityAlias = entityAlias;
                this.ImageType = (int)imageType;

                if (attributes != null && attributes.Length > 0)
                {
                    this.Attributes = string.Join(",", attributes.Select(x => PluginStepConfig<T>.GetMemberName(x))).ToLower();
                }
                else
                {
                    this.Attributes = null;
                }
            }
        }


        private static string GetMemberName(Expression<Func<T, object>> lambda)
        {
            MemberExpression body = lambda.Body as MemberExpression;

            if (body == null)
            {
                UnaryExpression ubody = (UnaryExpression)lambda.Body;
                body = ubody.Operand as MemberExpression;
            }

            return body.Member.Name;
        }
    }

    class AnyEntity : Entity
    {
        public AnyEntity() : base("") { }
    }


    /**
     * Enums to help setup plugin steps
     */
    public enum ExecutionMode
    {
        Synchronous = 0,
        Asynchronous = 1,
    }

    public enum ExecutionStage
    {
        PreValidation = 10,
        PreOperation = 20,
        PostOperation = 40,
    }

    public enum Deployment
    {
        ServerOnly = 0,
        MicrosoftDynamicsCRMClientforOutlookOnly = 1,
        Both = 2,
    }

    public enum EventOperation
    {
        Unknown = 0,
        Create = 1,
        Read = 2,
        Update = 4,
        Delete = 8,
        SetState = 16,
        Associate = 32,
        Disassociate = 64,
        Retrieve = 128,
        RetrieveMultiple = 256,
        SetStateDynamicEntity = 512,
    }

    public enum ImageType
    {
        PreImage = 0,
        PostImage = 1,
        Both = 2,
    }
    #endregion
}
