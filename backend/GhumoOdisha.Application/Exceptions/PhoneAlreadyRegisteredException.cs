using System.Net;

namespace GhumoOdisha.Application.Exceptions;

public class PhoneAlreadyRegisteredException()
    : AppException("An account with this phone number already exists.", HttpStatusCode.Conflict);
