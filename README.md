# RandomMessageCouldNotBeDispatchedToAnyHandlers

Attempt to reproduce issue reported [here](https://github.com/rebus-org/Rebus/issues/770) (random `MessageCouldNotBeDispatchedToAnyHandlersException` occurring during busy periods, where an exception is sometimes experienced).

At the time of writing, more than 4 million messages have been processed, spiced up with an exception for about one in every thousand. 

The reproduction has not been successful, so far.
