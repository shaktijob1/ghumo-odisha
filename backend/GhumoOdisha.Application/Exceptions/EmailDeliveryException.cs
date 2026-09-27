using System.Net;

namespace GhumoOdisha.Application.Exceptions;

public class EmailDeliveryException(string message = "Unable to send email right now. Please try again.")
    : AppException(message, HttpStatusCode.ServiceUnavailable);
