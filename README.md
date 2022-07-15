# The ReddWare Library Family

## ReddWare
Provides a set of general use classes and systems

### Individual classes
  * `ExpiringCache<KeyType, ContentType>`
    * Implements a caching system where items can expire and will automatically be disposed of when they do
  * `ListStack<T>`
    * Provides Stack and List like functionality for a given type
  * `ThreadedQueue<T>`
    * Provides a means of evenly distributing work among a set number of threads in a safe manner
  

### ReddWare.Langauge.Json
Provides a JSON implementation aimed at seamlessly handling inheritance in JSON.

Usage:
The following line converts the contents of `json` back into an `HDSLOutcomeSet` instance:
`var result = JsonConverter.GetObject<HDSLOutcomeSet>(json);`

This JSON implementation provides a `JsonIgnoreAttribute` to adorn properties and classes with.  Applying it to properties will see them ignored during the serialization and unserialization processes, while classes will be omitted from possible deserialization targets when resolving possible matches.


### ReddWare.IO.Parameters
Provides a command line argument parsing system for easy consumption of application's parameters.

Usage:
The following example defines a parameter handler, adds the argument rules to it, and then combs through a list of arguments for the first match to each of those rules.  All arguments that do not match rules are returned by `ph.Comb(args);`.

`ParameterHandler ph = new ParameterHandler();`
`ph.AddRules(`
    `new ParameterRuleOption("help", true, true, null, "-"),`
    `new ParameterRuleShortcut("ex"),`
    `new ParameterRuleFlag(new FlagDefinition[] {`
        `new FlagDefinition('e', true, true),`
        `new FlagDefinition('c', true, true),`
        `new FlagDefinition('s', true, false),` 
        `new FlagDefinition('r', true, true) }, "-")`
    `);`
`ph.Comb(args);`

In the above example, all three argument types are used:
  * `ParameterRuleOption` is used to define command line options.  In the example, the option defined is `-help: <params>`, where <params> is a comma seperated list of items.
  * `ParameterRuleShortcut` is used to define command line shortcuts.  In the example, the shortcut is `ex'<text>'`, where text is the argument's payload.
  * `ParameterRuleFlag` is used to define command line flags.  In the example, four flags are defined, each with a default value, and they are collectively set to use `-` as their indicator.  Flags toggle, so if the default is `false` then setting it will make the value `true`, and visa versa.  Note that in the example, `-ecsr` is valid, but so is `-e -cs`.

Once combed through, parameters can be consumed via methods on the ParameterHandler:
  * `ph.GetFlag("s")` will retrieve the `s` flag as a boolean value.
  * Because the `help` parameter accepts a comma list, it is possible for an array of items to be provided by the user.  To retrieve them, the GetAllParams method is used: `ph.GetAllParam("help")`.


### ReddWare.IO.Settings
Provides an INI file manager for the consumption, creation, and updating of .ini files.

Usage:
There are two basic ways of loading ini files: `IniFileManager.Discover` will look through an ini file and load everything it finds while `IniFileManager Explore` takes in an expected structure with default values and searches through the target file, populating what matches its provided structuring, adding what doesn't, and using default values for things that aren't found.  Ini files are stored in the resulting ini file manager through use of a key, allowing multiple files to be loaded and handled by a single manager.

In the following example, a file is explored.  
`var manager = IniFileManager.Explore(Ini_File_Location, true, false, false,`
    `new IniSubsection("HDSL_DB", null,`
        `new IniValue("DatabaseLocation", defaultValue: "file database.db")),`
    `new IniSubsection("HDSL_Web", null,`
        `new IniValue("BroadcastSources", defaultValue: null),`
        `new IniValue("TryExecuteRemotely", defaultValue: "False")));`
        
In the above example, the exploration searches the provided path, adds all newly discovered items, does not throw schema mismatch exceptions, and stores the resulting ini structure using the full path as the key, instead of just the file name.

To access loaded ini files, a simple query can be made.  Queries following the syntax `[file key]:[subsection]>[value]`.  The only potentially optional part is the file key, which can be omitted if only one file has been loaded.  If nothing is found, a null is returned.  The following is an example query:
  * `manager[@"HDSL_DB>DatabaseLocation"]?.Value` will query the value of `DatabaseLocation` in the subsection `HDSL_DB` within the default (only loaded) ini file.  

## ReddWare.Configurations
In the future, will provide a text-based notation for describing UIs and their functionality.


## ReddWare.Configurations.UI
In the future, will handle the  rendering of and interaction with UIs described via `ReddWare.Configurations'` functionality.