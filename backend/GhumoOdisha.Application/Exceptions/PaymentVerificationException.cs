using System.Net;

namespace GhumoOdisha.Application.Exceptions;

public class PaymentVerificationException(string message = "Payment verification failed.")
    : AppException(message, HttpStatusCode.BadRequest);
