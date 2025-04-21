using System.Globalization;

namespace Application.Helpers.Convertor;

public static class DateTimeHelper
{
    public static string ToShamsi(this DateTime date)
    {
        PersianCalendar pc = new PersianCalendar();
        int year = pc.GetYear(date);
        int month = pc.GetMonth(date);
        int day = pc.GetDayOfMonth(date);

        return $"{year:0000}/{month:00}/{day:00}";
    }
    
}