using System.Net;

namespace GhumoOdisha.Application.Exceptions;

public class WhatsAppDeliveryException()
    : AppException("Unable to send OTP right now. Please try again.", HttpStatusCode.ServiceUnavailable);
