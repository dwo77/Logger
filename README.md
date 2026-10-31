# Logger
In my Don-Quixote-esque attempt to get rid of external dependencies, I wrote a
simple replacement for log4net RollingFileAppender.

It works using the old entries in the .exe.config-file, but also reads from
simple param-key-values.

# Usage
1. Import namespace dwo.
2. Get the logger into your class with
    ```private static Logger _log = Logger.Instance;```
3. Use it like log4net, e.g. ```_log.Debug("my text");```


