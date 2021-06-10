using System;
using System.Globalization;
using Mariana.AVM2.Native;

using static Mariana.AVM2.Core.DateHelper;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The Date class represents date and time information.
    /// </summary>
    ///
    /// <remarks>
    /// The Date class represents any instant of time, with a precision of one millisecond. The
    /// Date class can represent dates and times from 20 April -271821 to 13 September 275760 in
    /// UTC. This class contains methods for date and time manipulation operations such as getting
    /// and setting individual components (year, month, day, hour etc.) and creating and parsing
    /// date strings, in both UTC and the system's local time zone.
    /// </remarks>
    [AVM2ExportClass(name = "Date", isDynamic = true, hasPrototypeMethods = true)]
    [AVM2ExportClassInternal(tag = ClassTag.DATE)]
    public sealed class ASDate : ASObject {

        //
        // This implementation of the Date class internally represents an instant of time
        // with a timestamp which is equal to the difference in milliseconds from the reference
        // value, which is taken to be 1 January -271820, 0:00:00.000 in UTC. This choice
        // of reference value ensures that all valid dates have positive timestamps, which
        // simplifies a lot of calculations involving modular arithmetic. The reason for
        // choosing a reference value lower than the minimum valid date value (which
        // is 20 April of the same year) is to ensure that timestamps remain positive
        // even after adding a (possibly negative) timezone offset for doing calculations
        // in local time.
        //

        /// <summary>
        /// The value of the "length" property of the AS3 Date class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 7;

        /// <summary>
        /// The value which is set to dates when validation fails after an operation.
        /// </summary>
        private const long INVALID_VALUE = -1;

        /// <summary>
        /// The value returned by the to...String() methods for invalid dates.
        /// </summary>
        private const string INVALID_STRING = "Invalid Date";

        /// <summary>
        /// The maximum date value in milliseconds, relative to the Unix epoch, as a double value.
        /// </summary>
        private const double MAX_DOUBLE_DATE_VALUE = 8640000000000000.0;

        /// <summary>
        /// The maximum integer value that can be exactly represented as a double, equal to 2^53-1
        /// </summary>
        private const double MAX_SAFE_DOUBLE_INT = 9007199254740991.0;

        private static readonly string[] s_toStringMonthNames = {
            "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec",
        };

        private static readonly string[] s_toStringWeekdayNames = {
            "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat",
        };

        /// <summary>
        /// A 64-bit integer containing the value of the Date object. For valid dates, this is a
        /// non-negative integer between <see cref="MIN_TIMESTAMP"/> and <see cref="MIN_TIMESTAMP"/>.
        /// </summary>
        private long m_value;

        /// <summary>
        /// Creates a new Date object representing the date and time at the instant when the
        /// constructor is called.
        /// </summary>
        public ASDate() : this(DateTime.UtcNow) {}

        /// <summary>
        /// Creates a new Date object whose value is given by the specified offset in milliseconds
        /// from the zero value (1 January 1970, 0:00:00 in UTC).
        /// </summary>
        /// <param name="offset">The offset from the zero value in milliseconds.</param>
        public ASDate(double offset) {
            m_value = (Math.Abs(offset) < MAX_DOUBLE_DATE_VALUE + 1.0) ? (long)offset + UNIX_ZERO_TIMESTAMP : INVALID_VALUE;
        }

        /// <summary>
        /// Creates a new Date object from a <see cref="DateTime"/> object.
        /// </summary>
        /// <param name="datetime">The DateTime object from which to create the new date. If the DateTime
        /// object's <see cref="DateTime.Kind" qualifyHint="true"/> property is not
        /// <see cref="DateTimeKind.Utc" qualifyHint="true"/>, the DateTime object is considered to
        /// be in local time.</param>
        public ASDate(DateTime datetime) {
            m_value = (long)(datetime.ToUniversalTime() - DateTime.UnixEpoch).TotalMilliseconds + UNIX_ZERO_TIMESTAMP;
            _internalCheckTimestamp();
        }

        /// <summary>
        /// Creates a Date object from a a date string.
        /// </summary>
        /// <param name="dateString">The date string to parse.</param>
        public ASDate(string dateString) {
            if (dateString == null) {
                m_value = UNIX_ZERO_TIMESTAMP;
                return;
            }

            bool isValid = DateParser.tryParse(dateString, out m_value);
            if (isValid)
                _internalCheckTimestamp();
            else
                m_value = INVALID_VALUE;
        }

        /// <summary>
        /// Creates a new date object with the specified date components.
        /// </summary>
        ///
        /// <param name="year">The year component.</param>
        /// <param name="month">The month component (zero-based).</param>
        /// <param name="day">The day component (day of the month).</param>
        /// <param name="hour">The hour component.</param>
        /// <param name="min">The minute component.</param>
        /// <param name="sec">The second component.</param>
        /// <param name="ms">The millisecond component.</param>
        /// <param name="isUTC">True if the date parameters are given in UTC, false if they are given
        /// in local time.</param>
        ///
        /// <remarks>
        /// If any parameter (other than <paramref name="year"/>) is out of range or has a negative
        /// value, it will underflow or overflow into preceding parameters. For example, if the minute
        /// parameter is 70, it will be considered as 10 and the hour parameter will be increased by
        /// 1.
        /// </remarks>
        public ASDate(
            double year, double month, double day, double hour = 0, double min = 0, double sec = 0, double ms = 0, bool isUTC = false)
        {
            m_value = _internalCreateTimestampFromComponents(year, month, day, hour, min, sec, ms, !isUTC);
        }

        /// <summary>
        /// This constructor implements the ActionScript 3 Date constructor.
        /// </summary>
        /// <param name="rest">The constructor arguments. This can contain a single date string, a
        /// single date value (offset in milliseconds from 1 January 1970, 0:00:00 in UTC) or
        /// individual date components (year, month, day, hour, minute, second and millisecond, in
        /// that order).</param>
        [AVM2ExportTrait]
        public ASDate(RestParam rest) {
            double year, month, day = 1, hour = 0, min = 0, sec = 0, ms = 0;

            switch (rest.length) {
                case 1: {
                    // Only one argument: a date value or a date string.
                    ASAny primitive = ASObject.AS_toPrimitive(rest[0].value);

                    if (primitive.value is ASString) {
                        bool isValidString = DateParser.tryParse((string)primitive, out m_value);
                        if (isValidString)
                            _internalCheckTimestamp();
                        else
                            m_value = INVALID_VALUE;
                    }
                    else {
                        setTime((double)primitive);
                    }
                    return;
                }

                // If more than one argument is given, then they are treated as
                // date components.
                default:
                    ms = (double)rest[6];
                    goto case 6;
                case 6:
                    sec = (double)rest[5];
                    goto case 5;
                case 5:
                    min = (double)rest[4];
                    goto case 4;
                case 4:
                    hour = (double)rest[3];
                    goto case 3;
                case 3:
                    day = (double)rest[2];
                    goto case 2;
                case 2:
                    month = (double)rest[1];
                    year = (double)rest[0];
                    break;
            }

            m_value = _internalCreateTimestampFromComponents(year, month, day, hour, min, sec, ms, isLocal: true);
        }

        private static long _internalCreateTimestampFromComponents(
            double year, double month, double day, double hour, double min, double sec, double ms, bool isLocal)
        {
            if (!Double.IsFinite(year) || !Double.IsFinite(month) || !Double.IsFinite(day)
                || !Double.IsFinite(hour) || !Double.IsFinite(min) || !Double.IsFinite(sec) || !Double.IsFinite(ms))
            {
                return INVALID_VALUE;
            }

            // Calculate the time component of the timestamp.
            double timeValue =
                Math.Truncate(hour) * MS_PER_HOUR + Math.Truncate(min) * MS_PER_MIN + Math.Truncate(sec) * MS_PER_SEC + Math.Truncate(ms);

            if (!Double.IsFinite(timeValue))
                return INVALID_VALUE;

            year = Math.Truncate(year);
            month = Math.Truncate(month);
            day = Math.Truncate(day);

            // 1900 must be added to two-digit years, as per ECMA 262.
            // This is for both the Date constructor and Date.UTC.
            if (year >= 0.0 && year <= 99.0)
                year += 1900.0;

            long timestamp;

            // If all components are representable as integers, we can avoid the expensive floating
            // point divisions for calculating the year and month offsets and do it using integer math.
            // We need a lower threshold for the year because it must not overflow when it is adjusted
            // for months outside [0, 11].

            const int safeIntegerYear = 1900000000;

            int iYear = (int)year;
            int iMonth = (int)month;
            int iDay = (int)day;

            if ((double)iYear == year
                && (double)iMonth == month
                && (double)iDay == day
                && iYear >= -safeIntegerYear
                && iYear <= safeIntegerYear
                && Math.Abs(timeValue) <= MAX_SAFE_DOUBLE_INT)
            {
                adjustMonthAndYear(ref iYear, ref iMonth);

                long days = getYearStartDaysFromZeroLong(iYear) + getMonthStartDayOfYear(iYear, iMonth) + iDay - 1;

                // Because |timeValue| is bounded at 2^53-1 on this path, it cannot "cancel out"
                // an overflow in days * MS_PER_DAY and give a valid timestamp.
                if (days > (Int64.MaxValue / MS_PER_DAY))
                    return INVALID_VALUE;

                timestamp = days * MS_PER_DAY + (long)timeValue;
            }
            else {
                // This is the slow path, where the date contribution to the timestamp is calculated using
                // floating point math.
                // Here we do all calculations relative to Unix zero and then add UNIX_ZERO_TIMESTAMP at
                // the end, otherwise results may be different from ECMAScript spec due to rounding.

                adjustMonthAndYearDouble(ref year, ref month);

                double yearOffset = getYearStartDaysFromUnixZeroDouble(year);
                double monthOffset = getMonthStartDayOfYear((int)month, isLeapYearDouble(year));
                double floatTimestamp = (yearOffset + monthOffset + day - 1.0) * MS_PER_DAY + timeValue;

                if (Math.Abs(floatTimestamp) > MAX_SAFE_DOUBLE_INT) {
                    // Early exit to ensure that the double-to-long conversion does not give an
                    // undefined result.
                    return INVALID_VALUE;
                }

                timestamp = (long)floatTimestamp + UNIX_ZERO_TIMESTAMP;
            }

            if (isLocal)
                timestamp = localTimestampToUniversal(timestamp);

            if ((ulong)(timestamp - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                return INVALID_VALUE;

            return timestamp;
        }

        private void _internalSetSecMs(in OptionalParam<double> sec, in OptionalParam<double> ms) {
            if (!sec.isSpecified && !ms.isSpecified)
                return;

            int iSec = (int)sec.value;
            int iMs = (int)ms.value;

            if ((double)iSec == Math.Truncate(sec.value) && (double)iMs == Math.Truncate(ms.value)) {
                // Fast path when the arguments are within the integer range.
                // This will be hit even if one of the arguments is unspecified, as
                // the value field will be zero.

                int currentSecPart, currentMsPart;
                currentSecPart = (int)(m_value % MS_PER_MIN);
                currentMsPart = currentSecPart % MS_PER_SEC;
                currentSecPart -= currentMsPart;

                if (sec.isSpecified)
                    m_value += ((long)iSec * MS_PER_SEC) - currentSecPart;
                if (ms.isSpecified)
                    m_value += iMs - currentMsPart;
            }
            else {
                _internalSetHourMinSecMsSlow(default, default, sec, ms);
            }
        }

        private void _internalSetMinSecMs(
            in OptionalParam<double> min, in OptionalParam<double> sec, in OptionalParam<double> ms)
        {
            if (!min.isSpecified && !sec.isSpecified && !ms.isSpecified)
                return;

            int iMin = (int)min.value;
            int iSec = (int)sec.value;
            int iMs = (int)ms.value;

            if ((double)iMin == Math.Truncate(min.value)
                && (double)iSec == Math.Truncate(sec.value)
                && (double)iMs == Math.Truncate(ms.value))
            {
                // Fast path when the arguments are within the integer range.
                // This will be hit even if some of the arguments are unspecified, as
                // the value field will be zero.

                int currentMinPart, currentSecPart, currentMsPart;
                currentMinPart = (int)(m_value % MS_PER_HOUR);
                currentSecPart = currentMinPart % MS_PER_MIN;
                currentMinPart -= currentSecPart;
                currentMsPart = currentSecPart % MS_PER_SEC;
                currentSecPart -= currentMsPart;

                if (min.isSpecified)
                    m_value += ((long)iMin * MS_PER_MIN) - currentMinPart;
                if (sec.isSpecified)
                    m_value += ((long)iSec * MS_PER_SEC) - currentSecPart;
                if (ms.isSpecified)
                    m_value += (long)iMs - currentMsPart;
            }
            else {
                _internalSetHourMinSecMsSlow(default, min, sec, ms);
            }
        }

        private void _internalSetHourMinSecMs(
            in OptionalParam<double> hour,
            in OptionalParam<double> min,
            in OptionalParam<double> sec,
            in OptionalParam<double> ms
        ) {
            if (!hour.isSpecified && !min.isSpecified && !sec.isSpecified && !ms.isSpecified)
                return;

            int iHour = (int)hour.value;
            int iMin = (int)min.value;
            int iSec = (int)sec.value;
            int iMs = (int)ms.value;

            if ((double)iHour == Math.Truncate(hour.value)
                && (double)iMin == Math.Truncate(min.value)
                && (double)iSec == Math.Truncate(sec.value)
                && (double)iMs == Math.Truncate(ms.value))
            {
                // Fast path when the arguments are within the integer range.
                // This will be hit even if some of the arguments are unspecified, as
                // the value field will be zero.

                int currentHourPart, currentMinPart, currentSecPart, currentMsPart;

                currentHourPart = (int)(m_value % MS_PER_DAY);
                currentMinPart = currentHourPart % MS_PER_HOUR;
                currentHourPart -= currentMinPart;
                currentSecPart = currentMinPart % MS_PER_MIN;
                currentMinPart -= currentSecPart;
                currentMsPart = currentSecPart % MS_PER_SEC;
                currentSecPart -= currentMsPart;

                if (hour.isSpecified)
                    m_value += ((long)iHour * MS_PER_HOUR) - currentHourPart;
                if (min.isSpecified)
                    m_value += ((long)iMin * MS_PER_MIN) - currentMinPart;
                if (sec.isSpecified)
                    m_value += ((long)iSec * MS_PER_SEC) - currentSecPart;
                if (ms.isSpecified)
                    m_value += (long)iMs - currentMsPart;
            }
            else {
                _internalSetHourMinSecMsSlow(hour, min, sec, ms);
            }
        }

        private void _internalSetHourMinSecMsSlow(
            in OptionalParam<double> hour,
            in OptionalParam<double> min,
            in OptionalParam<double> sec,
            in OptionalParam<double> ms
        ) {
            // Check for infinity/NaN. These will pass for unspecified arguments which evaluate to 0.
            if (!Double.IsFinite(hour.value) || !Double.IsFinite(min.value)
                || !Double.IsFinite(sec.value) || !Double.IsFinite(ms.value))
            {
                m_value = INVALID_VALUE;
                return;
            }

            double hourValue = Math.Truncate(hour.value);
            double minValue = Math.Truncate(min.value);
            double secValue = Math.Truncate(sec.value);
            double msValue = Math.Truncate(ms.value);

            int currentTimePart = (int)(m_value % MS_PER_DAY);
            int currentHourPart, currentMinPart, currentSecPart, currentMsPart;

            currentHourPart = (int)(m_value % MS_PER_DAY);
            currentMinPart = currentHourPart % MS_PER_HOUR;
            currentHourPart -= currentMinPart;
            currentSecPart = currentMinPart % MS_PER_MIN;
            currentMinPart -= currentSecPart;
            currentMsPart = currentSecPart % MS_PER_SEC;
            currentSecPart -= currentMsPart;

            double newTimePart = 0.0;
            newTimePart += hour.isSpecified ? hourValue * MS_PER_HOUR : (double)currentHourPart;
            newTimePart += min.isSpecified ? minValue * MS_PER_MIN : (double)currentMinPart;
            newTimePart += sec.isSpecified ? secValue * MS_PER_SEC : (double)currentSecPart;
            newTimePart += ms.isSpecified ? msValue : (double)currentMsPart;

            double floatTimestamp =
                (double)(m_value - currentTimePart - UNIX_ZERO_TIMESTAMP) + newTimePart;

            if (Math.Abs(floatTimestamp) > MAX_SAFE_DOUBLE_INT) {
                m_value = INVALID_VALUE;
                return;
            }

            m_value = (long)floatTimestamp + UNIX_ZERO_TIMESTAMP;
        }

        private void _internalSetMonthDay(in OptionalParam<double> month, in OptionalParam<double> day) {
            if (!day.isSpecified && !month.isSpecified)
                return;

            int iMonth = (int)month.value;
            int iDay = (int)day.value;

            if ((double)iMonth == Math.Truncate(month.value) && (double)iDay == Math.Truncate(day.value)) {
                // Fast path when the arguments are within the integer range.
                // This will be hit even if some of the arguments are unspecified, as
                // the value field will be zero.

                getDayMonthYearFromTimestamp(m_value, out int curYear, out int curMonth, out int curDay);
                int newYear = curYear;
                int newMonth = curMonth;
                int newDay = day.isSpecified ? iDay : curDay + 1;

                if (month.isSpecified) {
                    newMonth = iMonth;
                    adjustMonthAndYear(ref newYear, ref newMonth);
                }

                long totalAdjustDays = 0;

                if (newYear == curYear) {
                    // If there is no year change, avoid a relatively expensive getYearStartDaysFromZeroLong call.
                    bool curYearIsLeap = isLeapYear(curYear);

                    totalAdjustDays +=
                        getMonthStartDayOfYear(newMonth, curYearIsLeap) - getMonthStartDayOfYear(curMonth, curYearIsLeap);

                    if (day.isSpecified)
                        totalAdjustDays += (long)newDay - curDay;
                }
                else {
                    long currentTimePart = m_value % MS_PER_DAY;
                    m_value = currentTimePart;

                    totalAdjustDays = getYearStartDaysFromZeroLong(newYear) + getMonthStartDayOfYear(newYear, newMonth) + newDay - 1;
                }

                if (Math.Abs(totalAdjustDays) > (MAX_LOCAL_TIMESTAMP / MS_PER_DAY)) {
                    // Definitely an invalid date, as there is nothing to cancel out this addition.
                    m_value = INVALID_VALUE;
                    return;
                }

                m_value += totalAdjustDays * MS_PER_DAY;
            }
            else {
                _internalSetYearMonthDay(default, month, day);
            }
        }

        private void _internalSetYearMonthDay(
            in OptionalParam<double> year, in OptionalParam<double> month, in OptionalParam<double> day)
        {
            if (!year.isSpecified && !month.isSpecified && !day.isSpecified)
                return;

            int iYear = (int)year.value;
            int iMonth = (int)month.value;
            int iDay = (int)day.value;

            // We need a lower fast path threshold for the year because it must not overflow when it is adjusted
            // for months outside [0, 11].
            const int safeIntegerYear = 1900000000;

            if ((double)iYear == Math.Truncate(year.value)
                && (double)iMonth == Math.Truncate(month.value)
                && (double)iDay == Math.Truncate(day.value)
                && iYear >= -safeIntegerYear && iYear <= safeIntegerYear)
            {
                // Fast path when the arguments are within the integer range.
                // This will be hit even if some of the arguments are unspecified, as
                // the value field will be zero.

                int newYear, newMonth, newDay;

                if (year.isSpecified && month.isSpecified && day.isSpecified) {
                    // All three arguments are supplied. In this case we can avoid the getDayMonthYearFromTimestamp call.
                    newYear = iYear;
                    newMonth = iMonth;
                    newDay = iDay;
                    adjustMonthAndYear(ref newYear, ref newMonth);
                }
                else {
                    getDayMonthYearFromTimestamp(m_value, out int curYear, out int curMonth, out int curDay);

                    newYear = year.isSpecified ? iYear : curYear;
                    newDay = day.isSpecified ? iDay : curDay + 1;
                    newMonth = curMonth;

                    if (month.isSpecified) {
                        newMonth = iMonth;
                        adjustMonthAndYear(ref newYear, ref newMonth);
                    }
                }

                long newDatePartDays = getYearStartDaysFromZeroLong(newYear) + getMonthStartDayOfYear(newYear, newMonth) + newDay - 1;

                if (Math.Abs(newDatePartDays) > (MAX_LOCAL_TIMESTAMP / MS_PER_DAY)) {
                    // Definitely an invalid date, as there is nothing to cancel out this addition.
                    m_value = INVALID_VALUE;
                    return;
                }

                m_value = newDatePartDays * MS_PER_DAY + m_value % MS_PER_DAY;
            }
            else {
                _internalSetYearMonthDaySlow(year, month, day);
            }
        }

        private void _internalSetYearMonthDaySlow(
            in OptionalParam<double> year, in OptionalParam<double> month, in OptionalParam<double> day)
        {
            // Check for infinity/NaN. These will pass for unspecified arguments which evaluate to 0.
            if (!Double.IsFinite(year.value) || !Double.IsFinite(month.value) || !Double.IsFinite(day.value)) {
                m_value = INVALID_VALUE;
                return;
            }

            double yearValue = Math.Truncate(year.value);
            double monthValue = Math.Truncate(month.value);
            double dayValue = Math.Truncate(day.value);

            int currentTimePart = (int)(m_value % MS_PER_DAY);

            getDayMonthYearFromTimestamp(m_value, out int currentYear, out int currentMonth, out int currentDay);

            double newYear = year.isSpecified ? yearValue : (double)currentYear;
            double newMonth = month.isSpecified ? monthValue : (double)currentMonth;
            double newDay = day.isSpecified ? dayValue : (double)(currentDay + 1);

            adjustMonthAndYearDouble(ref newYear, ref newMonth);

            double yearOffset = getYearStartDaysFromUnixZeroDouble(newYear);
            double monthOffset = getMonthStartDayOfYear((int)newMonth, isLeapYearDouble(newYear));
            double newDatePart = yearOffset + monthOffset + newDay - 1.0;

            if (Math.Abs(newDatePart) > (double)(MAX_LOCAL_TIMESTAMP / MS_PER_DAY)) {
                // Definitely invalid becacuse currentTimePart cannot be negative.
                m_value = INVALID_VALUE;
                return;
            }

            m_value = (long)newDatePart * MS_PER_DAY + currentTimePart + UNIX_ZERO_TIMESTAMP;
        }

        /// <summary>
        /// Checks if the internal timestamp value in this instance is valid, and sets it to
        /// <see cref="INVALID_VALUE"/> if it is not.
        /// </summary>
        private void _internalCheckTimestamp() {
            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;
        }

        /// <summary>
        /// Creates a date value from the given date components in UTC.
        /// </summary>
        ///
        /// <param name="year">The year.</param>
        /// <param name="month">The month (zero-based).</param>
        /// <param name="day">The day of the month.</param>
        /// <param name="hour">The hour.</param>
        /// <param name="min">The minute.</param>
        /// <param name="sec">The second.</param>
        /// <param name="ms">The millisecond.</param>
        ///
        /// <returns>The date value, or NaN if one of the parameters is not a finite number or the
        /// result is outside the range of values permitted for Date objects.</returns>
        ///
        /// <remarks>
        /// <para>If any parameter (other than <paramref name="year"/>) is out of range or has a
        /// negative value, it will underflow or overflow into preceding parameters. For example, if
        /// the minute parameter is 70, it will be considered as 10 and the hour parameter will be
        /// increased by 1.</para>
        /// <para>This method returns a date value, not a Date object. To create a Date object, the
        /// returned value must be passed as an argument to the Date constructor.</para>
        /// </remarks>
        [AVM2ExportTrait]
        public static double UTC(
            double year, double month, double day = 1, double hour = 0, double min = 0, double sec = 0, double ms = 0)
        {
            long value = _internalCreateTimestampFromComponents(year, month, day, hour, min, sec, ms, isLocal: false);
            if (value == INVALID_VALUE)
                return Double.NaN;

            return (double)(value - UNIX_ZERO_TIMESTAMP);
        }

        /// <summary>
        /// Parses a date string and returns a date value that can be passed to the Date constructor
        /// to create a Date object. If the date string is invalid, NaN is returned.
        /// </summary>
        /// <param name="dateString">The date string to parse.</param>
        /// <returns>The date value computed by parsing <paramref name="dateString"/>.</returns>
        /// <remarks>
        /// Passing a string to the Date constructor is equivalent to calling this method, and then
        /// calling the Date constructor, passing the return value of this method to it.
        /// </remarks>
        [AVM2ExportTrait]
        public static double parse(string dateString) {
            bool isValid = DateParser.tryParse(dateString, out long timestamp);
            if (!isValid || (ulong)(timestamp - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                return Double.NaN;
            return (double)(timestamp - UNIX_ZERO_TIMESTAMP);
        }

        /// <summary>
        /// Gets the millisecond component of the Date object in UTC.
        /// </summary>
        /// <returns>The millisecond component of the Date object in UTC. This is NaN for invalid
        /// dates.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double getUTCMilliseconds() =>
            (m_value == INVALID_VALUE) ? Double.NaN : (double)(m_value % 1000);

        /// <summary>
        /// Sets the millisecond component of the Date object in UTC.
        /// </summary>
        /// <param name="ms">The millisecond component of the Date object in UTC. If this is default,
        /// leaves the millisecond component unchanged.</param>
        /// <returns>The new date value. If the date or any argument is invalid, returns
        /// NaN.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double setUTCMilliseconds(OptionalParam<double> ms = default) {
            if (ms.isSpecified)
                this.millisecondsUTC = ms.value;

            return getTime();
        }

        /// <summary>
        /// Gets the second component of the Date object in UTC.
        /// </summary>
        /// <returns>The second component of the Date object in UTC. This is NaN for invalid
        /// dates.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double getUTCSeconds() =>
            (m_value == INVALID_VALUE) ? Double.NaN : (double)((m_value / MS_PER_SEC) % 60);

        /// <summary>
        /// Sets the second (and optionally the millisecond) component of the Date object in UTC.
        /// </summary>
        /// <param name="sec">The second component of the Date object in UTC. If this is unspecified, leaves
        /// the second component unchanged.</param>
        /// <param name="ms">The millisecond component of the Date object in UTC. If this is unspecified,
        /// leaves the millisecond component unchanged.</param>
        /// <returns>The new date value. If the date or any argument is invalid, returns
        /// NaN.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double setUTCSeconds(OptionalParam<double> sec = default, OptionalParam<double> ms = default) {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            if (sec.isSpecified && !ms.isSpecified) {
                // Fast path
                this.secondsUTC = sec.value;
            }
            else {
                _internalSetSecMs(sec, ms);
                _internalCheckTimestamp();
            }

            return getTime();
        }

        /// <summary>
        /// Gets the minute component of the Date object in UTC.
        /// </summary>
        /// <returns>The minute component of the Date object in UTC. This is NaN for invalid
        /// dates.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double getUTCMinutes() =>
            (m_value == INVALID_VALUE) ? Double.NaN : (double)((m_value / MS_PER_MIN) % 60);

        /// <summary>
        /// Sets the minute (and optionally the second and millisecond) component of the Date object
        /// in UTC.
        /// </summary>
        ///
        /// <param name="min">The minute component of the Date object in UTC. If this is unspecified, leaves
        /// the minute component unchanged.</param>
        /// <param name="sec">The second component of the Date object in UTC. If this is unspecified, leaves
        /// the second component unchanged.</param>
        /// <param name="ms">The millisecond component of the Date object in UTC. If this is unspecified,
        /// leaves the millisecond component unchanged.</param>
        ///
        /// <returns>The new date value. If the date or any argument is invalid, returns
        /// NaN.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double setUTCMinutes(
            OptionalParam<double> min = default, OptionalParam<double> sec = default, OptionalParam<double> ms = default)
        {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            if (min.isSpecified && !sec.isSpecified && !ms.isSpecified) {
                // Fast path
                this.minutesUTC = min.value;
            }
            else {
                _internalSetMinSecMs(min, sec, ms);
                _internalCheckTimestamp();
            }

            return getTime();
        }

        /// <summary>
        /// Gets the hour component of the Date object in UTC.
        /// </summary>
        /// <returns>The second component of the Date object in UTC. This is NaN for invalid
        /// dates.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double getUTCHours() =>
            (m_value == INVALID_VALUE) ? Double.NaN : (double)((m_value / MS_PER_HOUR) % 24);

        /// <summary>
        /// Sets the hour (and optionally minute, second and millisecond) component of the Date object
        /// in UTC.
        /// </summary>
        ///
        /// <param name="hour">The hour component of the Date object in UTC. If this is unspecified, leaves
        /// the hour component unchanged.</param>
        /// <param name="min">The minute component of the Date object in UTC. If this is unspecified, leaves
        /// the minute component unchanged.</param>
        /// <param name="sec">The second component of the Date object in UTC. If this is unspecified, leaves
        /// the second component unchanged.</param>
        /// <param name="ms">The millisecond component of the Date object in UTC. If this is unspecified,
        /// leaves the millisecond component unchanged.</param>
        ///
        /// <returns>The new date value. If the date or any argument is invalid, returns
        /// NaN.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double setUTCHours(
            OptionalParam<double> hour = default,
            OptionalParam<double> min = default,
            OptionalParam<double> sec = default,
            OptionalParam<double> ms = default
        ) {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            if (hour.isSpecified && !min.isSpecified && !sec.isSpecified && !ms.isSpecified) {
                // Fast path
                this.hoursUTC = hour.value;
            }
            else {
                _internalSetHourMinSecMs(hour, min, sec, ms);
                _internalCheckTimestamp();
            }

            return getTime();
        }

        /// <summary>
        /// Gets the date component (day of the month) of the Date object in UTC.
        /// </summary>
        /// <returns>The day of the month of the Date object in UTC. This is NaN for invalid
        /// dates.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double getUTCDate() {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            getDayMonthYearFromTimestamp(m_value, out _, out _, out int date);
            return date + 1;  // getDayMonthYearFromTimestamp returns a zero-based date, so add 1 to it.
        }

        /// <summary>
        /// Sets the date component (day of the month) of the Date object in UTC.
        /// </summary>
        /// <param name="day">The day of the month of the Date object in UTC. If this is unspecified, leaves
        /// the date component unchanged.</param>
        /// <returns>The new date value. If the date or any argument is invalid, returns
        /// NaN.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double setUTCDate(OptionalParam<double> day = default) {
            if (day.isSpecified)
                this.dateUTC = day.value;

            return getTime();
        }

        /// <summary>
        /// Gets the month component of the Date object in UTC. The month is zero-based, i.e. January
        /// has the value 0.
        /// </summary>
        /// <returns>The month component of the Date object in UTC. This is NaN for invalid
        /// dates.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double getUTCMonth() {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            getDayMonthYearFromTimestamp(m_value, out _, out int month, out _);
            return month;
        }

        /// <summary>
        /// Sets the month (and optionally day) component of the Date object in UTC. The month is
        /// zero-based, i.e. January has the value 0.
        /// </summary>
        /// <param name="month">The month component of the Date object in UTC. If this is unspecified, leaves
        /// the month component unchanged.</param>
        /// <param name="day">The day of the month of the Date object in UTC. If this is unspecified, leaves
        /// the date component unchanged.</param>
        /// <returns>The new date value. If the date or any argument is invalid, returns
        /// NaN.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double setUTCMonth(
            OptionalParam<double> month = default, OptionalParam<double> day = default)
        {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            _internalSetMonthDay(month, day);
            _internalCheckTimestamp();

            return getTime();
        }

        /// <summary>
        /// Gets the year component of the Date object in UTC.
        /// </summary>
        /// <returns>The year component of the Date object in UTC. This is NaN for invalid
        /// dates.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double getUTCFullYear() {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            getYearAndDayFromTimestamp(m_value, out int year, out _);
            return year;
        }

        /// <summary>
        /// Sets the year (and optionally month and day) component of the Date object in UTC.
        /// </summary>
        ///
        /// <param name="year">The year component of the Date object in UTC. If this is unspecified, leaves the
        /// year component unchanged.</param>
        /// <param name="month">The month component of the Date object in UTC. If this is unspecified, leaves
        /// the month component unchanged.</param>
        /// <param name="day">The day of the month of the Date object in UTC. If this is unspecified, leaves
        /// the date component unchanged.</param>
        ///
        /// <returns>The new date value. If the date or any argument is invalid, returns
        /// NaN.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double setUTCFullYear(
            OptionalParam<double> year = default, OptionalParam<double> month = default, OptionalParam<double> day = default)
        {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            _internalSetYearMonthDay(year, month, day);
            _internalCheckTimestamp();

            return getTime();
        }

        /// <summary>
        /// Gets the day of the week Date object in UTC. The day of the week is zero-based, with 0
        /// representing Sunday.
        /// </summary>
        /// <returns>The day of the week of the Date object in UTC. This is NaN for invalid
        /// dates.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double getUTCDay() =>
            (m_value == INVALID_VALUE) ? Double.NaN : (double)(((int)(m_value / MS_PER_DAY) + 5) % 7);

        /// <summary>
        /// Returns the date value of the Date object.
        /// </summary>
        /// <returns>The date value of the Date object, or NaN for an invalid date.</returns>
        ///
        /// <remarks>
        /// The date value is the difference between the time represented by the Date object and the
        /// zero value (which corresponds to 1 January 1970, 0:00:00 in UTC) in milliseconds. This is
        /// positive for dates after the zero value, and negative for dates before the zero value.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double getTime() =>
            (m_value == INVALID_VALUE) ? Double.NaN : (double)(m_value - UNIX_ZERO_TIMESTAMP);

        /// <summary>
        /// Sets the date value of the Date object.
        /// </summary>
        /// <param name="v">The new date value. This must be a value from -8640000000000 to
        /// 8640000000000. If the value is outside this range or is infinity or NaN, the date is
        /// considered invalid and NaN is returned.</param>
        /// <returns>The new time value, or NaN if the date is invalid after setting its date
        /// value.</returns>
        ///
        /// <remarks>
        /// The date value is the difference between the time represented by the Date object and the
        /// zero value (which corresponds to 1 January 1970, 0:00:00 in UTC) in milliseconds. This is
        /// positive for dates after the zero value, and negative for dates before the zero value.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double setTime(double v) {
            // setTime() is the only method that can change an invalid date into a valid date.
            // It does not check the validity of the date before changing it.
            if (Double.IsNaN(v) || Math.Abs(v) >= MAX_DOUBLE_DATE_VALUE + 1.0)
                m_value = INVALID_VALUE;
            else
                m_value = (long)v + UNIX_ZERO_TIMESTAMP;

            return getTime();
        }

        /// <summary>
        /// Gets the millisecond component of the Date object in local time.
        /// </summary>
        /// <returns>The millisecond component of the Date object in local time. This is NaN for
        /// invalid dates.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double getMilliseconds() =>
            (m_value == INVALID_VALUE) ? Double.NaN : (double)(universalTimestampToLocal(m_value) % 1000);

        /// <summary>
        /// Sets the millisecond component of the Date object in local time.
        /// </summary>
        /// <param name="ms">The millisecond component of the Date object in local time. If this is
        /// unspecified, the millisecond component is left unchanged.</param>
        /// <returns>The new date value. If the date or any argument is invalid, returns
        /// NaN.</returns>
        [AVM2ExportPrototypeMethod]
        public double setMilliseconds(OptionalParam<double> ms = default) {
            if (ms.isSpecified)
                this.milliseconds = ms.value;

            return getTime();
        }

        /// <summary>
        /// Gets the second component of the Date object in local time.
        /// </summary>
        /// <returns>The second component of the Date object in local time. This is NaN for invalid
        /// dates.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double getSeconds() =>
            (m_value == INVALID_VALUE) ? Double.NaN : (double)((universalTimestampToLocal(m_value) / MS_PER_SEC) % 60);

        /// <summary>
        /// Sets the second (and optionally millisecond) component of the Date object in local time.
        /// </summary>
        /// <param name="sec">The second component of the Date object in local time. If this is unspecified,
        /// the second component is left unchanged.</param>
        /// <param name="ms">The millisecond component of the Date object in local time. If this is
        /// NaN, the millisecond component is left unchanged.</param>
        /// <returns>The new date value. If the date or any argument is invalid, returns
        /// NaN.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double setSeconds(OptionalParam<double> sec = default, OptionalParam<double> ms = default) {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            if (sec.isSpecified && !ms.isSpecified) {
                // Fast path
                this.seconds = sec.value;
            }
            else {
                m_value = universalTimestampToLocal(m_value);
                _internalSetSecMs(sec, ms);

                if (m_value == INVALID_VALUE)
                    return Double.NaN;

                m_value = localTimestampToUniversal(m_value);
                _internalCheckTimestamp();
            }

            return getTime();
        }

        /// <summary>
        /// Gets the minute component of the Date object in local time.
        /// </summary>
        /// <returns>The minute component of the Date object in local time. This is NaN for invalid
        /// dates.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double getMinutes() =>
            (m_value == INVALID_VALUE) ? Double.NaN : (double)((universalTimestampToLocal(m_value) / MS_PER_MIN) % 60);

        /// <summary>
        /// Sets the minute (and optionally second and millisecond) component of the Date object in
        /// local time.
        /// </summary>
        ///
        /// <param name="min">The minute component of the Date object in local time. If this is unspecified,
        /// the second component is left unchanged.</param>
        /// <param name="sec">The second component of the Date object in local time. If this is unspecified,
        /// the second component is left unchanged.</param>
        /// <param name="ms">The millisecond component of the Date object in local time. If this is
        /// NaN, the millisecond component is left unchanged.</param>
        ///
        /// <returns>The new date value. If the date or any argument is invalid, returns
        /// NaN.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double setMinutes(
            OptionalParam<double> min = default, OptionalParam<double> sec = default, OptionalParam<double> ms = default)
        {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            if (min.isSpecified && !sec.isSpecified && !ms.isSpecified) {
                // Fast path
                this.minutes = min.value;
            }
            else {
                m_value = universalTimestampToLocal(m_value);
                _internalSetMinSecMs(min, sec, ms);

                if (m_value == INVALID_VALUE)
                    return Double.NaN;

                m_value = localTimestampToUniversal(m_value);
                _internalCheckTimestamp();
            }

            return getTime();
        }

        /// <summary>
        /// Gets the hour component of the Date object in local time.
        /// </summary>
        /// <returns>The hour component of the Date object in local time. This is NaN for invalid
        /// dates.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double getHours() =>
            (m_value == INVALID_VALUE) ? Double.NaN : (double)((universalTimestampToLocal(m_value) / MS_PER_HOUR) % 24);

        /// <summary>
        /// Sets the hour (and optionally minute, second and millisecond) component of the Date object
        /// in local time.
        /// </summary>
        ///
        /// <param name="hour">The hour component of the Date object in local time. If this is unspecified,
        /// the hour component is left unchanged.</param>
        /// <param name="min">The minute component of the Date object in local time. If this is unspecified,
        /// the second component is left unchanged.</param>
        /// <param name="sec">The second component of the Date object in local time. If this is unspecified,
        /// the second component is left unchanged.</param>
        /// <param name="ms">The millisecond component of the Date object in local time. If this is
        /// NaN, the millisecond component is left unchanged.</param>
        ///
        /// <returns>The new date value. If the date or any argument is invalid, returns
        /// NaN.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double setHours(
            OptionalParam<double> hour = default,
            OptionalParam<double> min = default,
            OptionalParam<double> sec = default,
            OptionalParam<double> ms = default
        ) {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            if (hour.isSpecified && !min.isSpecified && !sec.isSpecified && !ms.isSpecified) {
                // Fast path
                this.hours = hour.value;
            }
            else {
                m_value = universalTimestampToLocal(m_value);
                _internalSetHourMinSecMs(hour, min, sec, ms);

                if (m_value == INVALID_VALUE)
                    return Double.NaN;

                m_value = localTimestampToUniversal(m_value);
                _internalCheckTimestamp();
            }

            return getTime();
        }

        /// <summary>
        /// Gets the date component of the Date object in local time.
        /// </summary>
        /// <returns>The date component of the Date object in local time. This is NaN for invalid
        /// dates.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double getDate() {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            getDayMonthYearFromTimestamp(universalTimestampToLocal(m_value), out _, out _, out int day);
            return (double)(day + 1);
        }

        /// <summary>
        /// Sets the date component (day of month) of the Date object in local time.
        /// </summary>
        /// <param name="day">The date component of the Date object in local time. If this is unspecified, the
        /// date component is left unchanged.</param>
        /// <returns>The new date value. If the date or any argument is invalid, returns
        /// NaN.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double setDate(OptionalParam<double> day = default) {
            if (day.isSpecified)
                this.dateUTC = day.value;

            return getTime();
        }

        /// <summary>
        /// Gets the month component of the Date object in local time.
        /// </summary>
        /// <returns>The month component of the Date object in local time. This is NaN for invalid
        /// dates.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double getMonth() {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            getDayMonthYearFromTimestamp(universalTimestampToLocal(m_value), out _, out int month, out _);
            return (double)month;
        }

        /// <summary>
        /// Sets the month (and optionally day) component of the Date object in local time. The month
        /// is zero-based, i.e. January has the value 0.
        /// </summary>
        /// <param name="month">The month component of the Date object in local time. If this is unspecified,
        /// the month component is left unchanged.</param>
        /// <param name="day">The date component of the Date object in local time. If this is unspecified, the
        /// date component is left unchanged.</param>
        /// <returns>The new date value. If the date or any argument is invalid, returns
        /// NaN.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double setMonth(OptionalParam<double> month = default, OptionalParam<double> day = default) {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            m_value = universalTimestampToLocal(m_value);
            _internalSetMonthDay(month, day);

            if (m_value == INVALID_VALUE)
                return Double.NaN;

            m_value = localTimestampToUniversal(m_value);
            _internalCheckTimestamp();

            return getTime();
        }

        /// <summary>
        /// Gets the year component of the Date object in local time.
        /// </summary>
        /// <returns>The year component of the Date object in local time. This is NaN for invalid
        /// dates.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double getFullYear() {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            getYearAndDayFromTimestamp(universalTimestampToLocal(m_value), out int year, out _);
            return (double)year;
        }

        /// <summary>
        /// Sets the year (and optionally month and day) component of the Date object in local time.
        /// The month is zero-based, i.e. January has the value 0.
        /// </summary>
        ///
        /// <param name="year">The year component of the Date object in local time. If this is unspecified,
        /// the year component is left unchanged.</param>
        /// <param name="month">The month component of the Date object in local time. If this is unspecified,
        /// the month component is left unchanged.</param>
        /// <param name="day">The date component of the Date object in local time. If this is unspecified, the
        /// date component is left unchanged.</param>
        ///
        /// <returns>The new date value. If the date or any argument is invalid, returns
        /// NaN.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double setFullYear(
            OptionalParam<double> year = default, OptionalParam<double> month = default, OptionalParam<double> day = default)
        {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            m_value = universalTimestampToLocal(m_value);
            _internalSetYearMonthDay(year, month, day);

            if (m_value == INVALID_VALUE)
                return Double.NaN;

            m_value = localTimestampToUniversal(m_value);
            _internalCheckTimestamp();

            return getTime();
        }

        /// <summary>
        /// Gets the day of the week Date object in local time. The day of the week is zero-based,
        /// with 0 representing Sunday.
        /// </summary>
        /// <returns>The day of the week of the Date object in local time. This is NaN for invalid
        /// dates.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double getDay() =>
            (m_value == INVALID_VALUE) ? Double.NaN : (double)(((int)(universalTimestampToLocal(m_value) / MS_PER_DAY) + 5) % 7);

        /// <summary>
        /// Gets the difference between the date and time represented by the current instance in UTC
        /// and the corresponding time in local time, in minutes.
        /// </summary>
        /// <returns>The difference between the UTC and local time values of the instant of time
        /// represented by the current instance in minutes.</returns>
        ///
        /// <remarks>
        /// Time zones behind UTC return positive offsets and time zones ahead of UTC return negative
        /// offsets. If the date value of the instance is in a daylight saving time interval, the
        /// daylight adjustment is also included in the offset.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double getTimezoneOffset() {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            return (double)((m_value - universalTimestampToLocal(m_value)) / MS_PER_MIN);
        }

        /// <summary>
        /// Gets or sets the millisecond component of the Date object in UTC.
        /// </summary>
        [AVM2ExportTrait]
        public double millisecondsUTC {
            get => getUTCMilliseconds();
            set {
                if (m_value == INVALID_VALUE)
                    return;

                if (Double.IsNaN(value) || Math.Abs(value) > MAX_SAFE_DOUBLE_INT) {
                    // Definitely invalid, nothing to cancel it out.
                    m_value = INVALID_VALUE;
                }
                else {
                    m_value = m_value - m_value % MS_PER_SEC + (long)value;
                    _internalCheckTimestamp();
                }
            }
        }

        /// <summary>
        /// Gets or sets the second component of the Date object in UTC.
        /// </summary>
        [AVM2ExportTrait]
        public double secondsUTC {
            get => getUTCSeconds();
            set {
                if (m_value == INVALID_VALUE)
                    return;

                if (Double.IsNaN(value) || Math.Abs(value) > MAX_SAFE_DOUBLE_INT) {
                    // Definitely invalid, nothing to cancel it out.
                    m_value = INVALID_VALUE;
                }
                else {
                    int currentSecPart = (int)(m_value % MS_PER_MIN);
                    m_value = m_value - currentSecPart + (currentSecPart % MS_PER_SEC) + (long)value * MS_PER_SEC;
                    _internalCheckTimestamp();
                }
            }
        }

        /// <summary>
        /// Gets or sets the minute component of the Date object in UTC.
        /// </summary>
        [AVM2ExportTrait]
        public double minutesUTC {
            get => getUTCMinutes();
            set {
                if (m_value == INVALID_VALUE)
                    return;

                if (Double.IsNaN(value) || Math.Abs(value) > MAX_SAFE_DOUBLE_INT) {
                    // Definitely invalid, nothing to cancel it out.
                    m_value = INVALID_VALUE;
                }
                else {
                    int currentMinPart = (int)(m_value % MS_PER_HOUR);
                    m_value = m_value - currentMinPart + (currentMinPart % MS_PER_MIN) + (long)value * MS_PER_MIN;
                    _internalCheckTimestamp();
                }
            }
        }

        /// <summary>
        /// Gets or sets the hour component of the Date object in UTC.
        /// </summary>
        [AVM2ExportTrait]
        public double hoursUTC {
            get => getUTCHours();
            set {
                if (m_value == INVALID_VALUE)
                    return;

                if (Double.IsNaN(value) || Math.Abs(value) > MAX_SAFE_DOUBLE_INT) {
                    // Definitely invalid, nothing to cancel it out.
                    m_value = INVALID_VALUE;
                }
                else {
                    int currentHourPart = (int)(m_value % MS_PER_DAY);
                    m_value = m_value - currentHourPart + (currentHourPart % MS_PER_HOUR) + (long)value * MS_PER_HOUR;
                    _internalCheckTimestamp();
                }
            }
        }

        /// <summary>
        /// Gets or sets the date component (day of month) of the Date object in UTC.
        /// </summary>
        [AVM2ExportTrait]
        public double dateUTC {
            get => getUTCDate();
            set {
                if (m_value == INVALID_VALUE)
                    return;

                if (Double.IsNaN(value) || Math.Abs(value) > (double)Int32.MaxValue) {
                    // Definitely invalid, nothing to cancel it out.
                    m_value = INVALID_VALUE;
                }
                else {
                    getDayMonthYearFromTimestamp(m_value, out _, out _, out int currentDay);
                    m_value += ((long)value - (currentDay + 1)) * MS_PER_DAY;
                    _internalCheckTimestamp();
                }
            }
        }

        /// <summary>
        /// Gets or sets the month component of the Date object in UTC.
        /// </summary>
        [AVM2ExportTrait]
        public double monthUTC {
            get => getUTCMonth();
            set => setUTCMonth(value);
        }

        /// <summary>
        /// Gets or sets the year component of the Date object in UTC.
        /// </summary>
        [AVM2ExportTrait]
        public double fullYearUTC {
            get => getUTCFullYear();
            set => setUTCFullYear(value);
        }

        /// <summary>
        /// Gets or sets the day of the week of the Date object in UTC.
        /// </summary>
        [AVM2ExportTrait]
        public double dayUTC => getUTCDay();

        /// <summary>
        /// Gets or sets the millisecond component of the Date object in local time.
        /// </summary>
        [AVM2ExportTrait]
        public double milliseconds {
            get => getMilliseconds();
            set {
                if (m_value == INVALID_VALUE)
                    return;

                if (Double.IsNaN(value) || Math.Abs(value) > MAX_SAFE_DOUBLE_INT) {
                    // Definitely invalid, nothing to cancel it out.
                    m_value = INVALID_VALUE;
                }
                else {
                    long localTimestamp = universalTimestampToLocal(m_value);
                    m_value = localTimestampToUniversal(localTimestamp - localTimestamp % MS_PER_SEC + (long)value);
                    _internalCheckTimestamp();
                }
            }
        }

        /// <summary>
        /// Gets or sets the second component of the Date object in local time.
        /// </summary>
        [AVM2ExportTrait]
        public double seconds {
            get => getSeconds();
            set {
                if (m_value == INVALID_VALUE)
                    return;

                if (Double.IsNaN(value) || Math.Abs(value) > MAX_SAFE_DOUBLE_INT) {
                    // Definitely invalid, nothing to cancel it out.
                    m_value = INVALID_VALUE;
                }
                else {
                    long localTimestamp = universalTimestampToLocal(m_value);
                    int currentSecPart = (int)(localTimestamp % MS_PER_MIN);

                    m_value = localTimestampToUniversal(
                        localTimestamp - currentSecPart + (currentSecPart % MS_PER_SEC) + (long)value * MS_PER_SEC
                    );
                    _internalCheckTimestamp();
                }
            }
        }

        /// <summary>
        /// Gets or sets the minute component of the Date object in local time.
        /// </summary>
        [AVM2ExportTrait]
        public double minutes {
            get => getMinutes();
            set {
                if (m_value == INVALID_VALUE)
                    return;

                if (Double.IsNaN(value) || Math.Abs(value) > MAX_SAFE_DOUBLE_INT) {
                    // Definitely invalid, nothing to cancel it out.
                    m_value = INVALID_VALUE;
                }
                else {
                    long localTimestamp = universalTimestampToLocal(m_value);
                    int currentMinPart = (int)(localTimestamp % MS_PER_HOUR);

                    m_value = localTimestampToUniversal(
                        localTimestamp - currentMinPart + (currentMinPart % MS_PER_MIN) + (long)value * MS_PER_MIN
                    );
                    _internalCheckTimestamp();
                }
            }
        }

        /// <summary>
        /// Gets or sets the hour component of the Date object in local time.
        /// </summary>
        [AVM2ExportTrait]
        public double hours {
            get => getHours();
            set {
                if (m_value == INVALID_VALUE)
                    return;

                if (Double.IsNaN(value) || Math.Abs(value) > MAX_SAFE_DOUBLE_INT) {
                    // Definitely invalid, nothing to cancel it out.
                    m_value = INVALID_VALUE;
                }
                else {
                    long localTimestamp = universalTimestampToLocal(m_value);
                    int currentHourPart = (int)(localTimestamp % MS_PER_DAY);
                    m_value = localTimestampToUniversal(
                        localTimestamp - currentHourPart + (currentHourPart % MS_PER_HOUR) + (long)value * MS_PER_HOUR
                    );
                    _internalCheckTimestamp();
                }
            }
        }

        /// <summary>
        /// Gets or sets the date component (day of month) of the Date object in local time.
        /// </summary>
        [AVM2ExportTrait]
        public double date {
            get => getDate();
            set {
                if (m_value == INVALID_VALUE)
                    return;

                if (Double.IsNaN(value) || Math.Abs(value) > (double)Int32.MaxValue) {
                    // Definitely invalid, nothing to cancel it out.
                    m_value = INVALID_VALUE;
                }
                else {
                    long localTimestamp = universalTimestampToLocal(m_value);
                    getDayMonthYearFromTimestamp(localTimestamp, out _, out _, out int currentDay);

                    long deltaDays = (long)value - (currentDay + 1);
                    m_value = localTimestampToUniversal(localTimestamp + deltaDays * MS_PER_DAY);
                    _internalCheckTimestamp();
                }
            }
        }

        /// <summary>
        /// Gets or sets the month component of the Date object in local time.
        /// </summary>
        [AVM2ExportTrait]
        public double month {
            get => getMonth();
            set => setMonth(value);
        }

        /// <summary>
        /// Gets or sets the year component of the Date object in local time.
        /// </summary>
        [AVM2ExportTrait]
        public double fullYear {
            get => getFullYear();
            set => setFullYear(value);
        }

        /// <summary>
        /// Gets or sets the day of the week of the Date object in local time.
        /// </summary>
        [AVM2ExportTrait]
        public double day => getDay();

        /// <summary>
        /// Gets or sets the date value of the Date object as a floating-point number.
        /// </summary>
        [AVM2ExportTrait]
        public double time {
            get => getTime();
            set => setTime(value);
        }

        /// <summary>
        /// Gets or sets the timezone offset of the Date object.
        /// </summary>
        [AVM2ExportTrait]
        public double timezoneOffset => getTimezoneOffset();

        /// <summary>
        /// Returns the primitive type representation of the object. For Date objects, this is the
        /// date value, i.e. the return value of the <c>getTime</c> method.
        /// </summary>
        /// <returns>A primitive value representation of the object.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public new double valueOf() => getTime();

        /// <summary>
        /// Returns a formatted string representation of the Date object.
        /// </summary>
        /// <returns>The string representation of the Date object.</returns>
        ///
        /// <remarks>
        /// This method is exported to the AVM2 with the name <c>toString</c>, but must be called
        /// from .NET code with the name <c>AS_toString</c> to avoid confusion with the
        /// <see cref="Object.ToString" qualifyHint="true"/> method.
        /// </remarks>
        [AVM2ExportTrait(name = "toString", nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod(name = "toString")]
        public new string AS_toString() {
            if (m_value == INVALID_VALUE)
                return INVALID_STRING;

            DateComponents components = getDateComponents();

            Span<char> stackBuffer = stackalloc char[40];
            var builder = new DateStringBuilder(stackBuffer);

            builder.writeString(s_toStringWeekdayNames[components.dayOfWeek]);
            builder.writeChar(' ');
            builder.writeString(s_toStringMonthNames[components.month]);
            builder.writeChar(' ');
            builder.writeInteger(components.day);
            builder.writeChar(' ');
            builder.writeTwoDigitInteger(components.hour);
            builder.writeChar(':');
            builder.writeTwoDigitInteger(components.minute);
            builder.writeChar(':');
            builder.writeTwoDigitInteger(components.second);
            builder.writeChar(' ');
            builder.writeTimeZoneOffset(components.timezoneOffset);
            builder.writeChar(' ');
            builder.writeInteger(components.year);

            return builder.makeString();
        }

        /// <summary>
        /// Returns a formatted string representation of the date part of the Date object.
        /// </summary>
        /// <returns>The formatted string representation of the date part of the Date
        /// object.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string toDateString() {
            if (m_value == INVALID_VALUE)
                return INVALID_STRING;

            DateComponents components = getDateComponents();

            Span<char> stackBuffer = stackalloc char[20];
            var builder = new DateStringBuilder(stackBuffer);

            builder.writeString(s_toStringWeekdayNames[components.dayOfWeek]);
            builder.writeChar(' ');
            builder.writeString(s_toStringMonthNames[components.month]);
            builder.writeChar(' ');
            builder.writeInteger(components.day);
            builder.writeChar(' ');
            builder.writeInteger(components.year);

            return builder.makeString();
        }

        /// <summary>
        /// Returns a formatted string representation of the time part of the Date object.
        /// </summary>
        /// <returns>The formatted string representation of the time part of the Date
        /// object.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string toTimeString() {
            if (m_value == INVALID_VALUE)
                return INVALID_STRING;

            DateComponents components = getDateComponents();

            Span<char> stackBuffer = stackalloc char[20];
            var builder = new DateStringBuilder(stackBuffer);

            builder.writeTwoDigitInteger(components.hour);
            builder.writeChar(':');
            builder.writeTwoDigitInteger(components.minute);
            builder.writeChar(':');
            builder.writeTwoDigitInteger(components.second);
            builder.writeChar(' ');
            builder.writeTimeZoneOffset(components.timezoneOffset);

            return builder.makeString();
        }

        /// <summary>
        /// Returns a formatted string representation of the time part of the Date object.
        /// </summary>
        /// <returns>The formatted string representation of the time part of the Date
        /// object.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string toUTCString() {
            if (m_value == INVALID_VALUE)
                return INVALID_STRING;

            DateComponents components = getDateComponents(utc: true);

            Span<char> stackBuffer = stackalloc char[32];
            var builder = new DateStringBuilder(stackBuffer);

            builder.writeString(s_toStringWeekdayNames[components.dayOfWeek]);
            builder.writeChar(' ');
            builder.writeString(s_toStringMonthNames[components.month]);
            builder.writeChar(' ');
            builder.writeInteger(components.day);
            builder.writeChar(' ');
            builder.writeTwoDigitInteger(components.hour);
            builder.writeChar(':');
            builder.writeTwoDigitInteger(components.minute);
            builder.writeChar(':');
            builder.writeTwoDigitInteger(components.second);
            builder.writeChar(' ');
            builder.writeInteger(components.year);
            builder.writeString(" UTC");

            return builder.makeString();
        }

        /// <summary>
        /// Returns a formatted string representation of the Date object according to the
        /// current locale.
        /// </summary>
        /// <returns>The formatted string representation of the Date object.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public new string toLocaleString() {
            if (m_value == INVALID_VALUE)
                return INVALID_STRING;

            DateComponents components = getDateComponents();

            if (components.year >= 1 && components.year <= 9999)
                return toDateTime().ToString("F", CultureInfo.CurrentCulture);

            // Anything which cannot be represented by DateTime is currently not
            // locale-formatted, but instead uses a locale-independent format similar
            // to toString() but without the time zone.

            Span<char> stackBuffer = stackalloc char[32];
            var builder = new DateStringBuilder(stackBuffer);

            builder.writeString(s_toStringWeekdayNames[components.dayOfWeek]);
            builder.writeChar(' ');
            builder.writeString(s_toStringMonthNames[components.month]);
            builder.writeChar(' ');
            builder.writeInteger(components.day);
            builder.writeChar(' ');
            builder.writeTwoDigitInteger(components.hour);
            builder.writeChar(':');
            builder.writeTwoDigitInteger(components.minute);
            builder.writeChar(':');
            builder.writeTwoDigitInteger(components.second);
            builder.writeChar(' ');
            builder.writeInteger(components.year);

            return builder.makeString();
        }

        /// <summary>
        /// Returns a formatted string representation of the date part of the Date object according to
        /// the current locale.
        /// </summary>
        /// <returns>The formatted string representation of the date part of the Date
        /// object.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string toLocaleDateString() {
            if (m_value == INVALID_VALUE)
                return INVALID_STRING;

            DateComponents components = getDateComponents();

            if (components.year >= 1 && components.year <= 9999)
                return toDateTime().ToString("D", CultureInfo.CurrentCulture);

            // TODO: Anything which cannot be represented by DateTime is currently
            // not locale-formatted.

            Span<char> stackBuffer = stackalloc char[24];
            var builder = new DateStringBuilder(stackBuffer);

            builder.writeString(s_toStringWeekdayNames[components.dayOfWeek]);
            builder.writeChar(' ');
            builder.writeString(s_toStringMonthNames[components.month]);
            builder.writeChar(' ');
            builder.writeInteger(components.day);
            builder.writeChar(' ');
            builder.writeInteger(components.year);

            return builder.makeString();
        }

        /// <summary>
        /// Returns a formatted string representation of the time part of the Date object according to
        /// the current locale.
        /// </summary>
        /// <returns>The formatted string representation of the time part of the Date
        /// object.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string toLocaleTimeString() {
            if (m_value == INVALID_VALUE)
                return INVALID_STRING;

            DateComponents components = getDateComponents();

            if (components.year >= 1 && components.year <= 9999)
                return toDateTime().ToString("T", CultureInfo.CurrentCulture);

            // TODO: Anything which cannot be represented by DateTime is currently
            // not locale-formatted.

            Span<char> stackBuffer = stackalloc char[16];
            var builder = new DateStringBuilder(stackBuffer);

            builder.writeTwoDigitInteger(components.hour);
            builder.writeChar(':');
            builder.writeTwoDigitInteger(components.minute);
            builder.writeChar(':');
            builder.writeTwoDigitInteger(components.second);

            return builder.makeString();
        }

        /// <summary>
        /// Returns the object that is used in place of the Date instance in JSON output.
        /// </summary>
        /// <param name="key">The name of the object property of which this object is the
        /// value.</param>
        /// <returns>The object that is used in place of the Date instance in JSON. For a Date object,
        /// this method calls the <c>toString()</c> method and returns its value.</returns>
        //[AVM2ExportTrait(nsName = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASAny toJSON(ASAny key) => AS_toString();

        /*
         * These methods are are not exported to AVM2.
         */

        /// <summary>
        /// Gets a Boolean value indicating whether the Date object represents a valid date.
        /// </summary>
        public bool isValidDate => m_value != INVALID_VALUE;

        /// <summary>
        /// Gets a Boolean value indicating whether the Date object represents a time in a daylight
        /// saving time interval in the local time zone.
        /// </summary>
        public bool isDaylightTime => (universalTimestampToLocal(m_value) - m_value) != localTimezoneOffset;

        /// <summary>
        /// Converts the current Date object into a BCL <see cref="DateTime"/> object.
        /// </summary>
        /// <param name="utc">If this is true, returns a <see cref="DateTime"/> object representing
        /// the UTC time, otherwise the returned <see cref="DateTime"/> object represents the time
        /// instant in local time. The <see cref="DateTimeKind"/> value of the returned
        /// <see cref="DateTime"/> instance will be set appropriately.</param>
        /// <returns>The <see cref="DateTime"/> instance corresponding to the same time instant as
        /// this Date instance.</returns>
        /// <remarks>
        /// An exception is thrown if the Date instance is invalid or out of the date range that can be
        /// represented by a <see cref="DateTime"/> object.
        /// </remarks>
        public DateTime toDateTime(bool utc = false) {
            long msFromUnixEpoch = (utc ? m_value : universalTimestampToLocal(m_value)) - UNIX_ZERO_TIMESTAMP;
            DateTime dt = DateTime.UnixEpoch.AddMilliseconds(msFromUnixEpoch);

            if (!utc)
                dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);

            return dt;
        }

        /// <summary>
        /// Gets all the components of the date.
        /// </summary>
        /// <param name="utc">If set to true, return the components in UTC, otherwise, return the
        /// components in local time.</param>
        /// <returns>A <see cref="DateComponents"/> structure containing all the components of the
        /// time instant represented by this <see cref="ASDate"/> instance.</returns>
        ///
        /// <exception cref="AVM2Exception">TypeError #10241: This <see cref="ASDate"/> instance
        /// represents an invalid date.</exception>
        ///
        /// <remarks>
        /// When many components of a date are required at the same time, using this method is faster
        /// than using the properties or methods for each of the individual components.
        /// </remarks>
        public DateComponents getDateComponents(bool utc = false) {
            if (m_value == INVALID_VALUE)
                throw ErrorHelper.createError(ErrorCode.MARIANA__INVALID_DATE_GET_COMPONENTS);

            long timestamp = utc ? m_value : universalTimestampToLocal(m_value);
            int datePart = (int)(timestamp / MS_PER_DAY);
            int timePart = (int)(timestamp - (long)datePart * MS_PER_DAY);

            getDayMonthYearFromTimestamp(timestamp, out int year, out int month, out int day);

            int hour = timePart / MS_PER_HOUR;
            int minSecMs = timePart - hour * MS_PER_HOUR;
            int minute = minSecMs / MS_PER_MIN;
            int secMs = minSecMs - minute * MS_PER_MIN;
            int second = secMs / MS_PER_SEC;
            int ms = secMs - second * MS_PER_SEC;

            int timezoneOffset = utc ? 0 : (int)((m_value - timestamp) / MS_PER_MIN);
            bool isDaylight = !utc && timezoneOffset != localTimezoneOffset;

            int monthOffset = getMonthStartDayOfYear(year, month);
            int dayOfYear = monthOffset + day;
            int dayOfWeek = (datePart + 5) % 7;

            return new DateComponents(year, month, day + 1, hour, minute, second, ms, dayOfYear, dayOfWeek, timezoneOffset, utc, isDaylight);
        }

        /// <exclude/>
        ///
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABC to IL compiler to invoke the ActionScript Date class constructor. This must not be called
        /// by outside .NET code. Date objects constructed from .NET code must use the constructor
        /// defined on the <see cref="ASDate"/> type.
        /// </summary>
        internal static new ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) => (new ASDate()).AS_toString();

        /// <summary>
        /// Used by <see cref="AS_toString"/> and related methods to build date and time strings.
        /// </summary>
        private ref struct DateStringBuilder {

            private Span<char> m_buffer;
            private int m_position;

            public DateStringBuilder(Span<char> buffer) {
                m_buffer = buffer;
                m_position = 0;
            }

            /// <summary>
            /// Appends a single character to the buffer.
            /// </summary>
            /// <param name="ch">The character to append.</param>
            public void writeChar(char ch) {
                m_buffer[m_position] = ch;
                m_position++;
            }

            /// <summary>
            /// Appends a span of characters to the buffer.
            /// </summary>
            /// <param name="str">The span to append.</param>
            public void writeString(ReadOnlySpan<char> str) {
                str.CopyTo(m_buffer.Slice(m_position));
                m_position += str.Length;
            }

            /// <summary>
            /// Appends an integer value to the buffer.
            /// </summary>
            /// <param name="value">The integer value.</param>
            public void writeInteger(int value) {
                if (value < 0) {
                    writeChar('-');
                    value = -value;
                }

                int startPosition = m_position;
                do {
                    int nextValue = value / 10;
                    int digit = value - nextValue * 10;
                    writeChar((char)('0' + digit));
                    value = nextValue;
                } while (value != 0);

                m_buffer.Slice(startPosition, m_position - startPosition).Reverse();
            }

            /// <summary>
            /// Writes a positive integer to the buffer using exactly two digits.
            /// </summary>
            /// <param name="value">The integer value.</param>
            public void writeTwoDigitInteger(int value) {
                int digit1 = value / 10;
                m_buffer[m_position] = (char)('0' + digit1);
                m_buffer[m_position + 1] = (char)('0' + value - digit1 * 10);
                m_position += 2;
            }

            /// <summary>
            /// Appends a string representation of the given time zone offset.
            /// </summary>
            /// <param name="offsetMin">The time zone offset behind UTC, in minutes.</param>
            public void writeTimeZoneOffset(int offsetMin) {
                writeString("GMT");

                if (offsetMin > 0) {
                    writeChar('-');
                }
                else {
                    writeChar('+');
                    offsetMin = -offsetMin;
                }

                int offsetHours = offsetMin / 60;
                offsetMin -= offsetHours * 60;

                int digit1, digit2, digit3, digit4;
                digit1 = offsetHours / 10;
                digit2 = offsetHours - 10 * digit1;
                digit3 = offsetMin / 10;
                digit4 = offsetMin - 10 * digit3;

                Span<char> span = m_buffer.Slice(m_position, 4);
                m_position += 4;

                span[3] = (char)('0' + digit4);
                span[2] = (char)('0' + digit3);
                span[1] = (char)('0' + digit2);
                span[0] = (char)('0' + digit1);
            }

            /// <summary>
            /// Creates a string from the current contents of the buffer.
            /// </summary>
            /// <returns>The created string.</returns>
            public string makeString() => new string(m_buffer.Slice(0, m_position));

        }

    }

}
