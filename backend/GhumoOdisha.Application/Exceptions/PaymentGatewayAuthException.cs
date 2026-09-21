using System.Net;

namespace GhumoOdisha.Application.Exceptions;

public class PaymentGatewayAuthException()
    : AppException("Payment gateway authentication failed.", HttpStatusCode.Unauthorized);
