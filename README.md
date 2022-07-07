# The ReddWare Library Family

## ReddWare
Provides a set of general use classes and systems

  * `ExpiringCache<KeyType, ContentType>`
    * Implements a caching system where items can expire and will automatically be disposed of when they do
  * `ListStack<T>`
    * Provides Stack and List like functionality for a given type
  * JSON Library (`namespace ReddWare.Langauge.Json`)
    * Provides a JSON implementation aimed at seamlessly handling inheritance in JSON.
  * `ThreadedQueue<T>`
    * Provides a means of evenly distributing work among a set number of threads in a safe manner

## ReddWare.Configurations
In the future, will provide a text-based notation for describing UIs and their functionality.


## ReddWare.Configurations.UI
In the future, will handle the  rendering of and interaction with UIs described via `ReddWare.Configurations'` functionality.