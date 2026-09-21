using System.Net;

namespace GhumoOdisha.Application.Exceptions;

public class PaymentGatewayException(string message = "Unable to reach the payment gateway right now. Please try again.")
    : AppException(message, HttpStatusCode.InternalServerError);
