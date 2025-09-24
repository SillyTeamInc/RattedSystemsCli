namespace RattedSystemsCli.Utilities;


// exceptions specifically for the cli; the message will be printed directly to the user with no stack trace
public class EmiException(string message) : Exception(message);