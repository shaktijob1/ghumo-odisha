using System.Net;

namespace GhumoOdisha.Application.Exceptions;

public class InvalidCredentialsException(string message = "Phone number or PIN is incorrect.")
    : AppException(message, HttpStatusCode.Unauthorized);
