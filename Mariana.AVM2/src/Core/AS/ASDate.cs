using System;
using System.Globalization;
using System.Text;
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
            m_value = (Math.Abs(offset) <= 8640000000000000.0) ? (long)offset + UNIX_ZERO_TIMESTAMP : INVALID_VALUE;
        }

        /// <summary>
        /// Creates a new Date object from a <see cref="System.DateTime"/> object.
        /// </summary>
        /// <param name="datetime">The DateTime object from which to create the new date. If the DateTime
        /// object's <see cref="DateTime.Kind" qualifyHint="true"/> property is not
        /// <see cref="DateTimeKind.Utc" qualifyHint="true"/>, the DateTime object is considered to
        /// be in local time.</param>
        public ASDate(DateTime datetime) {
            m_value = (long)(datetime.ToUniversalTime() - unixZeroAsDateTime).TotalMilliseconds + UNIX_ZERO_TIMESTAMP;
            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;
        }

        /// <summary>
        /// Creates a Date object from a a date string.
        /// </summary>
        /// <param name="dateString">The date string to parse.</param>
        public ASDate(string dateString) {
            if (dateString == null) {
                m_value = UNIX_ZERO_TIMESTAMP;
            }
            else {
                bool isValid = DateParser.tryParse(dateString, out m_value);
                if (!isValid || (ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                    m_value = INVALID_VALUE;
            }
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
            double year, double month, double day,
            double hour = 0, double min = 0, double sec = 0, double ms = 0,
            bool isUTC = false)
        {
            if (!_validateDateComponent(year)
                || !_validateDateComponent(month)
                || !_validateDateComponent(day)
                || !_validateTimeComponent(hour)
                || !_validateTimeComponent(min)
                || !_validateTimeComponent(sec)
                || !_validateTimeComponent(ms))
            {
                return;
            }

            m_value = createTimestamp((int)year, (int)month, (int)day - 1, (long)hour, (long)min, (long)sec, (long)ms, !isUTC);

            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;
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
                case 1:
                    // Only one argument: a date value or a date string.
                    if (rest[0].value is ASString) {
                        bool success = DateParser.tryParse((string)rest[0], out m_value);
                        if (!success || (ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                            m_value = INVALID_VALUE;
                    }
                    else {
                        setTime((double)rest[0]);
                    }
                    return;

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

            if (!_validateDateComponent(year)
                || !_validateDateComponent(month)
                || !_validateDateComponent(day)
                || !_validateTimeComponent(hour)
                || !_validateTimeComponent(min)
                || !_validateTimeComponent(sec)
                || !_validateTimeComponent(ms))
            {
                return;
            }

            m_value = createTimestamp(
                (int)year, (int)month, (int)day - 1, (long)hour, (long)min, (long)sec, (long)ms, isLocal: true);

            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;
        }

        /// <summary>
        /// This method is used to validate time components (hour, minute, second, millisecond)
        /// passed to methods which modify the date. If validation fails, the date value is set
        /// to the invalid value.
        /// </summary>
        /// <param name="d">The argument to validate.</param>
        /// <returns>True if validation succeeds, false if it fails.</returns>
        private bool _validateTimeComponent(double d) {
            if (Math.Abs(d) <= 9007199254740991d)
                return true;

            m_value = INVALID_VALUE;
            return false;
        }

        /// <summary>
        /// This method is used to validate time components (hour, minute, second, millisecond)
        /// that are used to create a date.
        /// </summary>
        /// <param name="d">The argument to validate.</param>
        /// <returns>True if validation succeeds, false if it fails.</returns>
        private static bool _isValidTimeComponent(double d) => Math.Abs(d) <= 9007199254740991d;

        /// <summary>
        /// This method is used to validate date components (year, month, day) passed to methods
        /// which modify the date value. If validation fails, the date value is set to the invalid value.
        /// </summary>
        /// <param name="d">The argument to validate.</param>
        /// <returns>True if validation succeeds, false if it fails.</returns>
        private bool _validateDateComponent(double d) {
            if (Math.Abs(d) <= 2147483647d)
                return true;

            m_value = INVALID_VALUE;
            return false;
        }

        /// <summary>
        /// This method is used to validate date components (year, month, day)
        /// that are used to create a date.
        /// </summary>
        /// <param name="d">The argument to validate.</param>
        /// <returns>True if validation succeeds, false if it fails.</returns>
        private static bool _isValidDateComponent(double d) => Math.Abs(d) <= 2147483647d;

        private void _internalSetSecMs(in OptionalParam<double> sec, in OptionalParam<double> ms) {
            if ((sec.isSpecified && !_validateTimeComponent(sec.value))
                || (ms.isSpecified && !_validateTimeComponent(ms.value)))
            {
                return;
            }

            long oldSec, oldMs;
            oldSec = m_value % MS_PER_MIN;
            oldMs = (int)oldSec % MS_PER_SEC;
            oldSec -= oldMs;

            long newSec = sec.isSpecified ? (long)sec.value * MS_PER_SEC : oldSec;
            long newMs = ms.isSpecified ? (long)ms.value : oldMs;
            m_value = m_value + (newSec - oldSec) + (newMs - oldMs);
        }

        private void _internalSetMinSecMs(
            in OptionalParam<double> min, in OptionalParam<double> sec, in OptionalParam<double> ms)
        {
            if ((min.isSpecified && !_validateTimeComponent(min.value))
                || (sec.isSpecified && !_validateTimeComponent(sec.value))
                || (ms.isSpecified && !_validateTimeComponent(ms.value)))
            {
                return;
            }

            long oldMin, oldSec, oldMs;
            oldMin = m_value % MS_PER_HOUR;
            oldSec = (int)oldMin % MS_PER_MIN;
            oldMin -= oldSec;
            oldMs = (int)oldSec % MS_PER_SEC;
            oldSec -= oldMs;

            long newMin = min.isSpecified ? (long)min.value * MS_PER_MIN : oldMin;
            long newSec = sec.isSpecified ? (long)sec.value * MS_PER_SEC : oldSec;
            long newMs = ms.isSpecified ? (long)ms.value : oldMs;

            m_value = m_value + (newMin - oldMin) + (newSec - oldSec) + (newMs - oldMs);
        }

        private void _internalSetHourMinSecMs(
            in OptionalParam<double> hour,
            in OptionalParam<double> min,
            in OptionalParam<double> sec,
            in OptionalParam<double> ms
        ) {
            if ((hour.isSpecified && !_validateTimeComponent(hour.value))
                || (min.isSpecified && !_validateTimeComponent(min.value))
                || (sec.isSpecified && !_validateTimeComponent(sec.value))
                || (ms.isSpecified && !_validateTimeComponent(ms.value)))
            {
                return;
            }

            long oldHour, oldMin, oldSec, oldMs;

            oldHour = m_value % MS_PER_DAY;
            oldMin = (int)oldHour % MS_PER_HOUR;
            oldHour -= oldMin;
            oldSec = (int)oldMin % MS_PER_MIN;
            oldMin -= oldSec;
            oldMs = (int)oldSec % MS_PER_SEC;
            oldSec -= oldMs;

            long newHour = hour.isSpecified ? (long)hour.value * MS_PER_HOUR : oldHour;
            long newMin = min.isSpecified ? (long)min.value * MS_PER_MIN : oldMin;
            long newSec = sec.isSpecified ? (long)sec.value * MS_PER_SEC : oldSec;
            long newMs = ms.isSpecified ? (long)ms.value : oldMs;

            m_value = m_value + (newHour - oldHour) + (newMin - oldMin) + (newSec - oldSec) + (newMs - oldMs);
        }

        private void _internalSetMonthDay(in OptionalParam<double> month, in OptionalParam<double> day) {
            if ((day.isSpecified && !_validateDateComponent(day.value))
                || (month.isSpecified && !_validateDateComponent(month.value)))
            {
                return;
            }

            getDayMonthYearFromTimestamp(m_value, out int currentYear, out int currentMonth, out int currentDay);

            int monthAdjDays = 0;

            if (month.isSpecified) {
                int newMonth = (int)month.value;
                int yearAfterMonthAdjust = currentYear;

                if (adjustMonthAndYear(ref yearAfterMonthAdjust, ref newMonth))
                    monthAdjDays += getYearStartDayDiff(currentYear, yearAfterMonthAdjust);

                monthAdjDays += getMonthStartDayOfYear(yearAfterMonthAdjust, newMonth);
                monthAdjDays -= getMonthStartDayOfYear(currentYear, currentMonth);
            }

            long dayAdjustDays = day.isSpecified ? (long)((int)day.value - currentDay - 1) : 0L;

            m_value += ((long)monthAdjDays + dayAdjustDays) * MS_PER_DAY;
        }

        private void _internalSetYearMonthDay(
            in OptionalParam<double> year, in OptionalParam<double> month, in OptionalParam<double> day)
        {
            if ((year.isSpecified && !_validateDateComponent(year.value))
                || (month.isSpecified && !_validateDateComponent(month.value))
                || (day.isSpecified && !_validateDateComponent(day.value)))
            {
                return;
            }

            if (month.isSpecified || day.isSpecified) {
                getDayMonthYearFromTimestamp(m_value, out int curYear, out int curMonth, out int curDay);

                int newYear, yearAdjDays;
                if (!year.isSpecified) {
                    newYear = curYear;
                    yearAdjDays = 0;
                }
                else {
                    newYear = (int)year.value;
                    yearAdjDays = getYearStartDayDiff(curYear, newYear);
                }

                int monthAdjDays = 0;
                if (month.isSpecified) {
                    int newMonth = (int)month.value;
                    int newYearAfterMonthAdjust = newYear;

                    if (adjustMonthAndYear(ref newYearAfterMonthAdjust, ref newMonth))
                        monthAdjDays += getYearStartDayDiff(newYear, newYearAfterMonthAdjust);

                    monthAdjDays += getMonthStartDayOfYear(newYearAfterMonthAdjust, newMonth);
                    monthAdjDays -= getMonthStartDayOfYear(newYear, curMonth);
                }

                long dayAdjDays = day.isSpecified ? (long)((int)day.value - curDay - 1) : 0;

                m_value += ((long)yearAdjDays + (long)monthAdjDays + dayAdjDays) * MS_PER_DAY;
            }
            else {
                if (!year.isSpecified)
                    return;

                getYearAndDayFromTimestamp(m_value, out int curYear, out _);
                m_value += (long)getYearStartDayDiff((int)year.value, curYear) * MS_PER_DAY;
            }
        }

        /// <summary>
        /// Appends the string representation of the given integer to a StringBuilder. This is used by
        /// <see cref="AS_toString"/> and related methods.
        /// </summary>
        /// <param name="sb">The <see cref="StringBuilder"/> to which to append the
        /// integer.</param>
        /// <param name="val">The integer value.</param>
        private void _writeIntegerToSB(StringBuilder sb, int val) {
            if (val < 0) {
                sb.Append('-');
                val = -val;
            }

            int div = 1;
            while (true) {
                int div2 = div * 10;
                if (div2 > val)
                    break;
                div = div2;
            }

            do {
                int digit = val / div;
                sb.Append((char)('0' + digit));
                val -= digit * div;
                div /= 10;
            } while (div != 0);
        }

        /// <summary>
        /// Appends the string representation of the given two-digit positive integer to a
        /// StringBuilder. This is used by <see cref="AS_toString"/> and related methods.
        /// </summary>
        /// <param name="sb">The <see cref="StringBuilder"/> to which to append the
        /// integer.</param>
        /// <param name="val">The integer value. This must be between 0 and 99. For numbers between 0
        /// and 9, a leading zero is written.</param>
        private void _writeTwoDigitIntegerToSB(StringBuilder sb, int val) {
            int digit1 = val / 10;
            sb.Append((char)('0' + digit1));
            sb.Append((char)('0' + val - digit1 * 10));
        }

        /// <summary>
        /// Appends a string representation of the given time zone offset to a StringBuilder. This is
        /// used by <see cref="AS_toString"/> and related methods.
        /// </summary>
        /// <param name="sb">The <see cref="StringBuilder"/> to which to append the time zone
        /// offset.</param>
        /// <param name="offsetMin">The time zone offset behind UTC, in minutes.</param>
        private void _writeTimeZoneOffsetToSB(StringBuilder sb, int offsetMin) {
            sb.Append("GMT");

            if (offsetMin > 0) {
                sb.Append('-');
            }
            else {
                sb.Append('+');
                offsetMin = -offsetMin;
            }

            int offsetHours = offsetMin / 60;
            offsetMin -= offsetHours * 60;

            int digit1, digit2, digit3, digit4;
            digit1 = offsetHours / 10;
            digit2 = offsetHours - 10 * digit1;
            digit3 = offsetMin / 10;
            digit4 = offsetMin - 10 * digit3;
            sb.Append((char)('0' + digit1));
            sb.Append((char)('0' + digit2));
            sb.Append((char)('0' + digit3));
            sb.Append((char)('0' + digit4));
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
            if (!_isValidDateComponent(year)
                || !_isValidDateComponent(month)
                || !_isValidDateComponent(day)
                || !_isValidTimeComponent(hour)
                || !_isValidTimeComponent(min)
                || !_isValidTimeComponent(sec)
                || !_isValidTimeComponent(ms))
            {
                return Double.NaN;
            }

            long value = createTimestamp(
                (int)year, (int)month, (int)day - 1, (long)hour, (long)min, (long)sec, (long)ms, isLocal: false);

            if (value < MIN_TIMESTAMP || value > MAX_TIMESTAMP)
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
            if (m_value == INVALID_VALUE || !ms.isSpecified || !_validateTimeComponent(ms.value))
                return getTime();

            m_value = m_value - m_value % 1000L + (long)ms.value;

            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;

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

            _internalSetSecMs(sec, ms);

            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;

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

            _internalSetMinSecMs(min, sec, ms);

            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;

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

            _internalSetHourMinSecMs(hour, min, sec, ms);

            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;

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
            if (m_value == INVALID_VALUE || !day.isSpecified || !_validateDateComponent(day.value))
                return getTime();

            getDayMonthYearFromTimestamp(m_value, out _, out _, out int currentDay);

            m_value += (long)((int)day.value - currentDay - 1) * MS_PER_DAY;

            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;

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

            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;

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

            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;

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
        public double getUTCDay() {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            getYearAndDayFromTimestamp(m_value, out int year, out int dayOfYear);
            return (getYearBeginWeekDay(year) + dayOfYear) % 7;
        }

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
            if (!_validateTimeComponent(v))
                return Double.NaN;

            m_value = (long)v + UNIX_ZERO_TIMESTAMP;

            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;

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
            if (m_value == INVALID_VALUE || !ms.isSpecified || !_validateTimeComponent(ms.value))
                return getTime();

            long localValue = universalTimestampToLocal(m_value);
            m_value = localTimestampToUniversal(localValue - localValue % 1000 + (long)ms.value);

            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;

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

            m_value = universalTimestampToLocal(m_value);
            _internalSetSecMs(sec, ms);

            if (m_value == INVALID_VALUE)
                return Double.NaN;

            m_value = localTimestampToUniversal(m_value);
            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;

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

            m_value = universalTimestampToLocal(m_value);
            _internalSetMinSecMs(min, sec, ms);

            if (m_value == INVALID_VALUE)
                return Double.NaN;

            m_value = localTimestampToUniversal(m_value);
            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;

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
            OptionalParam<double> ms = default)
        {
            if (m_value == INVALID_VALUE)
                return Double.NaN;

            m_value = universalTimestampToLocal(m_value);
            _internalSetHourMinSecMs(hour, min, sec, ms);

            if (m_value == INVALID_VALUE)
                return Double.NaN;

            m_value = localTimestampToUniversal(m_value);
            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;

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
            if (m_value == INVALID_VALUE || !day.isSpecified || !_validateDateComponent(day.value))
                return getTime();

            long localValue = universalTimestampToLocal(m_value);
            getDayMonthYearFromTimestamp(localValue, out _, out _, out int currentDay);

            m_value = localTimestampToUniversal(localValue + (long)((int)day.value - currentDay - 1) * MS_PER_DAY);

            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;

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
            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;

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
            if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                m_value = INVALID_VALUE;

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
        public double getDay() {
            if (m_value == INVALID_VALUE)
                return Double.NaN;
            getYearAndDayFromTimestamp(universalTimestampToLocal(m_value), out int year, out int ord);
            return (double)((getYearBeginWeekDay(year) + ord) % 7);
        }

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
                if (m_value == INVALID_VALUE || !_validateTimeComponent(value))
                    return;

                m_value = m_value - m_value % MS_PER_SEC + (long)value;

                if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                    m_value = INVALID_VALUE;
            }
        }

        /// <summary>
        /// Gets or sets the second component of the Date object in UTC.
        /// </summary>
        [AVM2ExportTrait]
        public double secondsUTC {
            get => getUTCSeconds();
            set {
                if (m_value == INVALID_VALUE || !_validateTimeComponent(value))
                    return;

                int oldSecs = (int)(m_value % MS_PER_MIN);
                m_value = m_value - oldSecs + (long)value * MS_PER_SEC + (oldSecs % MS_PER_SEC);

                if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                    m_value = INVALID_VALUE;
            }
        }

        /// <summary>
        /// Gets or sets the minute component of the Date object in UTC.
        /// </summary>
        [AVM2ExportTrait]
        public double minutesUTC {
            get => getUTCMinutes();
            set {
                if (m_value == INVALID_VALUE || !_validateTimeComponent(value))
                    return;

                int oldMins = (int)(m_value % MS_PER_HOUR);
                m_value = m_value - oldMins + (long)value * MS_PER_MIN + (oldMins % MS_PER_MIN);

                if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                    m_value = INVALID_VALUE;
            }
        }

        /// <summary>
        /// Gets or sets the hour component of the Date object in UTC.
        /// </summary>
        [AVM2ExportTrait]
        public double hoursUTC {
            get => getUTCHours();
            set {
                if (m_value == INVALID_VALUE || !_validateTimeComponent(value))
                    return;

                int oldHours = (int)(m_value % MS_PER_DAY);
                m_value = m_value - oldHours + (long)value * MS_PER_HOUR + (oldHours % MS_PER_HOUR);

                if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                    m_value = INVALID_VALUE;
            }
        }

        /// <summary>
        /// Gets or sets the date component (day of month) of the Date object in UTC.
        /// </summary>
        [AVM2ExportTrait]
        public double dateUTC {
            get => getUTCDate();
            set {
                if (m_value == INVALID_VALUE || !_validateDateComponent(value))
                    return;

                getDayMonthYearFromTimestamp(m_value, out _, out _, out int currentDay);
                m_value += (long)((int)value - currentDay - 1) * MS_PER_DAY;

                if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                    m_value = INVALID_VALUE;
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
                if (m_value == INVALID_VALUE || !_validateTimeComponent(value))
                    return;

                long local = universalTimestampToLocal(m_value);
                m_value = localTimestampToUniversal(local - local % MS_PER_SEC + (long)value);

                if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                    m_value = INVALID_VALUE;
            }
        }

        /// <summary>
        /// Gets or sets the second component of the Date object in local time.
        /// </summary>
        [AVM2ExportTrait]
        public double seconds {
            get => getSeconds();
            set {
                if (m_value == INVALID_VALUE || !_validateTimeComponent(value))
                    return;

                long local = universalTimestampToLocal(m_value);
                int oldSecs = (int)(local % MS_PER_MIN);
                m_value = localTimestampToUniversal(local - oldSecs + (long)value * MS_PER_SEC + (local % MS_PER_SEC));

                if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                    m_value = INVALID_VALUE;
            }
        }

        /// <summary>
        /// Gets or sets the minute component of the Date object in local time.
        /// </summary>
        [AVM2ExportTrait]
        public double minutes {
            get => getMinutes();
            set {
                if (m_value == INVALID_VALUE || !_validateTimeComponent(value))
                    return;

                long local = universalTimestampToLocal(m_value);
                int oldMins = (int)(local % MS_PER_HOUR);
                m_value = localTimestampToUniversal(local - oldMins + (long)value * MS_PER_MIN + (local % MS_PER_MIN));

                if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                    m_value = INVALID_VALUE;
            }
        }

        /// <summary>
        /// Gets or sets the hour component of the Date object in local time.
        /// </summary>
        [AVM2ExportTrait]
        public double hours {
            get => getHours();
            set {
                if (m_value == INVALID_VALUE || !_validateTimeComponent(value))
                    return;

                long local = universalTimestampToLocal(m_value);
                int oldHours = (int)(local % MS_PER_DAY);
                m_value = localTimestampToUniversal(local - oldHours + (long)value * MS_PER_HOUR + (local % MS_PER_HOUR));

                if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                    m_value = INVALID_VALUE;
            }
        }

        /// <summary>
        /// Gets or sets the date component (day of month) of the Date object in local time.
        /// </summary>
        [AVM2ExportTrait]
        public double date {
            get => getDate();
            set {
                if (m_value == INVALID_VALUE || !_validateDateComponent(value))
                    return;

                long local = universalTimestampToLocal(m_value);
                getDayMonthYearFromTimestamp(local, out _, out _, out int currentDay);

                m_value = localTimestampToUniversal(local + (long)((int)value - currentDay - 1) * MS_PER_DAY);

                if ((ulong)(m_value - MIN_TIMESTAMP) > TIMESTAMP_RANGE)
                    m_value = INVALID_VALUE;
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

            Span<int> components = stackalloc int[11];
            getDateComponents(components);
            StringBuilder sb = new StringBuilder();

            sb.Append(s_toStringWeekdayNames[components[10]]);
            sb.Append(' ');
            sb.Append(s_toStringMonthNames[components[1]]);
            sb.Append(' ');
            _writeIntegerToSB(sb, components[2]);
            sb.Append(' ');
            _writeTwoDigitIntegerToSB(sb, components[3]);
            sb.Append(':');
            _writeTwoDigitIntegerToSB(sb, components[4]);
            sb.Append(':');
            _writeTwoDigitIntegerToSB(sb, components[5]);
            sb.Append(' ');
            _writeTimeZoneOffsetToSB(sb, components[7]);
            sb.Append(' ');
            _writeIntegerToSB(sb, components[0]);

            return sb.ToString();
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

            Span<int> components = stackalloc int[11];
            getDateComponents(components);
            StringBuilder sb = new StringBuilder();

            sb.Append(s_toStringWeekdayNames[components[10]]);
            sb.Append(' ');
            sb.Append(s_toStringMonthNames[components[1]]);
            sb.Append(' ');
            _writeIntegerToSB(sb, components[2]);
            sb.Append(' ');
            _writeIntegerToSB(sb, components[0]);

            return sb.ToString();
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

            Span<int> components = stackalloc int[11];
            getDateComponents(components);
            StringBuilder sb = new StringBuilder();

            _writeTwoDigitIntegerToSB(sb, components[3]);
            sb.Append(':');
            _writeTwoDigitIntegerToSB(sb, components[4]);
            sb.Append(':');
            _writeTwoDigitIntegerToSB(sb, components[5]);
            sb.Append(' ');
            _writeTimeZoneOffsetToSB(sb, components[7]);

            return sb.ToString();
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

            Span<int> components = stackalloc int[11];
            getDateComponents(components, true);
            StringBuilder sb = new StringBuilder();

            sb.Append(s_toStringWeekdayNames[components[10]]);
            sb.Append(' ');
            sb.Append(s_toStringMonthNames[components[1]]);
            sb.Append(' ');
            _writeIntegerToSB(sb, components[2]);
            sb.Append(' ');
            _writeTwoDigitIntegerToSB(sb, components[3]);
            sb.Append(':');
            _writeTwoDigitIntegerToSB(sb, components[4]);
            sb.Append(':');
            _writeTwoDigitIntegerToSB(sb, components[5]);
            sb.Append(' ');
            _writeIntegerToSB(sb, components[0]);
            sb.Append(" UTC");

            return sb.ToString();
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

            Span<int> components = stackalloc int[11];
            getDateComponents(components);

            if (components[0] >= 1 && components[0] <= 9999)
                return toDateTime().ToString("F", CultureInfo.CurrentCulture);

            // Anything which cannot be represented by DateTime is currently not
            // locale-formatted, but instead uses a locale-independent format similar
            // to toString() but without the time zone.

            StringBuilder sb = new StringBuilder();

            sb.Append(s_toStringWeekdayNames[components[10]]);
            sb.Append(' ');
            sb.Append(s_toStringMonthNames[components[1]]);
            sb.Append(' ');
            _writeIntegerToSB(sb, components[2]);
            sb.Append(' ');
            _writeTwoDigitIntegerToSB(sb, components[3]);
            sb.Append(':');
            _writeTwoDigitIntegerToSB(sb, components[4]);
            sb.Append(':');
            _writeTwoDigitIntegerToSB(sb, components[5]);
            sb.Append(' ');
            _writeIntegerToSB(sb, components[0]);

            return sb.ToString();
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

            Span<int> components = stackalloc int[11];
            getDateComponents(components);

            if (components[0] >= 1 && components[0] <= 9999)
                return toDateTime().ToString("D", CultureInfo.CurrentCulture);

            // TODO: Anything which cannot be represented by DateTime is currently
            // not locale-formatted.

            StringBuilder sb = new StringBuilder();

            sb.Append(s_toStringWeekdayNames[components[10]]);
            sb.Append(' ');
            sb.Append(s_toStringMonthNames[components[1]]);
            sb.Append(' ');
            _writeIntegerToSB(sb, components[2]);
            sb.Append(' ');
            _writeIntegerToSB(sb, components[0]);

            return sb.ToString();
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

            Span<int> components = stackalloc int[11];
            getDateComponents(components);

            if (components[0] >= 1 && components[0] <= 9999)
                return toDateTime().ToString("T", CultureInfo.CurrentCulture);

            // TODO: Anything which cannot be represented by DateTime is currently
            // not locale-formatted.

            StringBuilder sb = new StringBuilder();

            _writeTwoDigitIntegerToSB(sb, components[3]);
            sb.Append(':');
            _writeTwoDigitIntegerToSB(sb, components[4]);
            sb.Append(':');
            _writeTwoDigitIntegerToSB(sb, components[5]);

            return sb.ToString();
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
            DateTime dt = unixZeroAsDateTime.AddMilliseconds(
                (utc ? m_value : universalTimestampToLocal(m_value)) - UNIX_ZERO_TIMESTAMP);

            if (!utc)
                dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);

            return dt;
        }

        /// <summary>
        /// Gets all the components of the date.
        /// </summary>
        /// <param name="components">A span into which the components are to be written.</param>
        /// <param name="utc">If set to true, return the components in UTC, otherwise, return the
        /// components in local time.</param>
        ///
        /// <remarks>
        /// <para>
        /// The date components are written in the following order: year, month (zero-based),
        /// day of the month, hour, minute, second, millisecond, timezone offset in minutes (positive
        /// for behind UTC, 0 if <paramref name="utc"/> is true), whether the time is in daylight
        /// savings (0 if false, 1 if true, 0 if <paramref name="utc"/> is true), day of the year
        /// (zero-based), day of the week (zero-based, with Sunday as the first day). If the date is
        /// invalid, all components will be set to 0.
        /// </para>
        /// When many components of a date are required at the same time, using this method is faster
        /// than using the properties or methods for each of the individual components.
        /// </remarks>
        public void getDateComponents(Span<int> components, bool utc = false) {
            components = components.Slice(0, 11);

            if (m_value == INVALID_VALUE) {
                components.Fill(0);
                return;
            }

            long timestamp = utc ? m_value : universalTimestampToLocal(m_value);
            int timePart = (int)(timestamp % MS_PER_DAY);

            getDayMonthYearFromTimestamp(timestamp, out int year, out int month, out int day);

            components[0] = year;
            components[1] = month;
            components[2] = day + 1;

            int hour = timePart / MS_PER_HOUR;
            int minSecMs = timePart - hour * MS_PER_HOUR;
            int minute = minSecMs / MS_PER_MIN;
            int secMs = minSecMs - minute * MS_PER_MIN;
            int second = secMs / MS_PER_SEC;
            int ms = secMs - second * MS_PER_SEC;

            components[3] = hour;
            components[4] = minute;
            components[5] = second;
            components[6] = ms;

            components[7] = utc ? 0 : (int)((m_value - timestamp) / MS_PER_MIN);
            components[8] = (!utc && components[7] != localTimezoneOffset) ? 1 : 0;

            int monthOffset = getMonthStartDayOfYear(year, month);
            components[9] = monthOffset + day;
            components[10] = (getYearBeginWeekDay(year) + monthOffset + day) % 7;
        }

        /// <exclude/>
        ///
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABCIL compiler to invoke the ActionScript Date class constructor. This must not be called
        /// by outside .NET code. Date objects constructed from .NET code must use the constructor
        /// defined on the <see cref="ASDate"/> type.
        /// </summary>
        internal static new ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) => (new ASDate()).AS_toString();

    }

}
