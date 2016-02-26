# ExtendedPlugin.cs


Extended Plugin.cs which makes DAXIF# able to synchronize plugins to a CRM solution, without the use of the PluginRegistration tool.

Different version of the plugin.cs exist as DAXIF# PluginSync have been extended to incoporate more function in order to replace the use of 

## v2.2.0.7

Extended to handle user context for which a plugin can be executed under. Plugins can now accept a GUID of a user entity in the CRM system.
The GUID will be tested if it points to a valid user entity in the system

## v2.1+

Extended to handle more parameters for steps

 - Deployment Mode 
 - ExecutionMode
 - Step Name
 - ExecutionOrder 
 - FilteredAttributes

Images can also be added to a plugin with the parameters
 - Image Name
 - Entity Alias
 - ImageType
 - Attributes
