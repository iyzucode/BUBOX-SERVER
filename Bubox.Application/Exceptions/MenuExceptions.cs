namespace Bubox.Application.Exceptions;

public class MenuNotFoundException(string message = "Menu tidak ditemukan.")
    : Exception(message);

public class InvalidMenuDataException(string message = "Data menu tidak valid.")
    : Exception(message);
