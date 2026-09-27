using System.Net;

namespace GhumoOdisha.Application.Exceptions;

// 502, not 401: this is our server's Razorpay credentials being rejected, not the caller's session.
// A 401 here makes the Angular auth interceptor refresh/retry the customer's token and log admins out.
public class PaymentGatewayAuthException()
    : AppException("Online payment is temporarily unavailable. Please try again later.", HttpStatusCode.BadGateway);
