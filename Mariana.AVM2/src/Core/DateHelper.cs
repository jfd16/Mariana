using System;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// Contains methods used by the Date class for performing date calculations.
    /// </summary>
    /// <remarks>
    /// The timestamps used by the methods of this class are those used in the internal representation
    /// of the Date class, i.e. offsets in milliseconds from 1 January -271820, 0:00:00.000 UTC.
    /// </remarks>
    internal static class DateHelper {

        private static readonly short[] s_monthOffsets = {
            0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334, 365
        };

        private static readonly byte[] s_monthLookupTable = _createMonthLookupTable();

        private static int s_baseTimezoneOffset = (int)TimeZoneInfo.Local.BaseUtcOffset.TotalMilliseconds;

        private static DSTRule[] s_daylightRules = _importRulesFromCurrentTimeZone();

        public const int MS_PER_SEC = 1000;
        public const int MS_PER_MIN = MS_PER_SEC * 60;
        public const int MS_PER_HOUR = MS_PER_MIN * 60;
        public const int MS_PER_DAY = MS_PER_HOUR * 24;

        /// <summary>
        /// The minimum value in the permitted range of timestamps for Date objects.
        /// </summary>
        public const long MIN_TIMESTAMP = 9417600000;

        /// <summary>
        /// The maximum value in the permitted range of timestamps values for Date objects.
        /// </summary>
        public const long MAX_TIMESTAMP = MIN_TIMESTAMP + 17280000000000000;

        /// <summary>
        /// The difference between the minimum and maximum timestamp values.
        /// </summary>
        public const ulong TIMESTAMP_RANGE = (ulong)(MAX_TIMESTAMP - MIN_TIMESTAMP);

        /// <summary>
        /// The timestamp of the Unix zero date (which is also the "zero" date value in ECMAScript).
        /// To obtain the Unix/ECMAScript date value, subtract this from a timestamp.
        /// </summary>
        public const long UNIX_ZERO_TIMESTAMP = MIN_TIMESTAMP + 8640000000000000;

        /// <summary>
        /// The Unix/ECMAScript zero date as a <see cref="DateTime"/> instance.
        /// </summary>
        public static readonly DateTime unixZeroAsDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Gets the day of the week for January 1 of a given year. The value returned is a positive
        /// integer that is congruent modulo 7 to the day of the week (with Sunday being zero).
        /// </summary>
        ///
        /// <param name="year">The year.</param>
        /// <returns>A positive integer that is congruent modulo 7 to the day of the week of January 1
        /// of the given year.</returns>
        public static int getYearBeginWeekDay(int year) {
            // Here 1995 is taken as the reference year, as January 1 starts on a Sunday in that year.
            if (year < 1995) {
                // This method should not return a negative value, because the modulo operator considers
                // the quotient rounded towards zero.
                // To ensure that the result is always positive, the (negative) difference is added to an arbitrarily
                // large number that is divisible by 7 which will always give a positive result in the
                // valid range of years for Date objects.
                return 700000 + (year - 1995) + (year - 1996) / 4 - (year - 2000) / 100 + (year - 2000) / 400;
            }
            else {
                // Years 1995 and later will always give positive results, so day of week offset = number of years
                // + number of leap years.
                return (year - 1995) + (year - 1993) / 4 - (year - 2001) / 100 + (year - 2001) / 400;
            }
        }

        /// <summary>
        /// Adjusts the given month and year such that the month is a positive integer between 0 and
        /// 11. If the month is negative, years are subtracted; if it is greater than 11, years are
        /// added. For a month between 0 and 11, the month and year remain unchanged.
        /// </summary>
        ///
        /// <param name="month">The month.</param>
        /// <param name="year">The year.</param>
        /// <returns>True if the year and month were adjusted; false if they were not.</returns>
        public static bool adjustMonthAndYear(ref int year, ref int month) {
            if (month > 11) {
                int addYears = month / 12;
                year += addYears;
                month -= addYears * 12;
                return true;
            }
            if (month < 0) {
                year += (month + 1) / 12 - 1;
                month = (7200000 + month) % 12;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Creates the month lookup table used for finding the month from a given day of
        /// the year.
        /// </summary>
        /// <returns>The month lookup table.</returns>
        private static byte[] _createMonthLookupTable() {
            short[] offsets = s_monthOffsets;
            byte[] table = new byte[365];

            for (int i = 1; i < 12; i++) {
                int start = offsets[i], end = offsets[i + 1];
                table.AsSpan(start, end - start).Fill((byte)i);
            }

            return table;
        }

        /// <summary>
        /// Returns a value indicating whether the given year is a leap year.
        /// </summary>
        /// <param name="year">The year.</param>
        /// <returns>True if <paramref name="year"/> is a leap year, otherwise false.</returns>
        public static bool isLeapYear(int year) {
            if ((year & 3) != 0)
                return false;

            int yearDiv100 = year / 100;
            return year != yearDiv100 * 100 || (yearDiv100 & 3) == 0;
        }

        /// <summary>
        /// Returns the number of days in the given year.
        /// </summary>
        /// <param name="year">The year.</param>
        /// <returns>The number of days in <paramref name="year"/>.</returns>
        public static int getDaysInYear(int year) => isLeapYear(year) ? 366 : 365;

        /// <summary>
        /// Returns the day at which the given month starts in the given year, relative to the
        /// start of that year.
        /// </summary>
        /// <param name="year">The year.</param>
        /// <param name="month">The month of the year (zero-based).</param>
        /// <returns>The day at which the given month starts in the given year, relative to the
        /// start of that year.</returns>
        public static int getMonthStartDayOfYear(int year, int month) =>
            s_monthOffsets[month] + ((month >= 2 && isLeapYear(year)) ? 1 : 0);

        /// <summary>
        /// Returns the day at which the given month starts in a year, relative to the
        /// start of that year.
        /// </summary>
        /// <param name="month">The month of the year (zero-based).</param>
        /// <param name="isLeapYear">True for a leap year, false otherwise.</param>
        /// <returns>The day at which the given month starts in a leap or non-leap year,
        /// (depending on <paramref name="isLeapYear"/>), relative to the start of
        /// that year.</returns>
        public static int getMonthStartDayOfYear(int month, bool isLeapYear) =>
            s_monthOffsets[month] + ((month >= 2 && isLeapYear) ? 1 : 0);

        /// <summary>
        /// Returns the difference in days between 1 January of the specified year and the reference
        /// date of 1 January -271821.
        /// </summary>
        /// <param name="year">The year.</param>
        /// <returns>The difference in days between 1 January of the specified year and the reference
        /// date of 1 January -271821.</returns>
        public static int getYearStartDaysFromZero(int year) {
            return (year + 271821) * 365
                + ((year + 271823) >> 2)
                - (year + 271899) / 100
                + (year + 271999) / 400;
        }

        /// <summary>
        /// Returns the difference in days between 1 January of two years. Both years must be
        /// not less than -271821.
        /// </summary>
        /// <param name="fromYear">The first year.</param>
        /// <param name="toYear">The second year.</param>
        /// <returns>The difference in days between the beginning of <paramref name="toYear"/>
        /// and <paramref name="fromYear"/>.</returns>
        public static int getYearStartDayDiff(int fromYear, int toYear) {
            return (toYear - fromYear) * 365
                + ((toYear + 271823) >> 2) - ((fromYear + 271823) >> 2)
                - ((toYear + 271899) / 100) + ((fromYear + 271899) / 100)
                + ((toYear + 271999) / 400) - ((fromYear + 271999) / 400);
        }

        /// <summary>
        /// Calculates the year and ordinal date (difference between the date and 1 January of the
        /// year) from the given timestamp and writes them to the <paramref name="year"/> and
        /// <paramref name="dayOfYear"/> parameters.
        /// </summary>
        ///
        /// <param name="timestamp">The timestamp value.</param>
        /// <param name="year">The year.</param>
        /// <param name="dayOfYear">The day of the year.</param>
        public static void getYearAndDayFromTimestamp(long timestamp, out int year, out int dayOfYear) {
            int days = (int)(timestamp / MS_PER_DAY);

            // To get the actual year, first make an estimate of the year by assuming that one year
            // has exactly 365.2425 days.
            year = (int)((double)days / 365.2425) - 271821;

            // The correction can be made to the estimated year by checking the day-of-year with
            // respect to the estimated year.
            // If this is negative, the correct year is taken to be the preceding year.
            // If this is greater than or equal to the number of days in the estimated year, the correct
            // year is taken to be the succeeding year.
            // Otherwise, the estimated year is correct.
            dayOfYear = days - getYearStartDaysFromZero(year);
            while (true) {
                if (dayOfYear < 0) {
                    year--;
                    dayOfYear += getDaysInYear(year);
                    continue;
                }

                int daysInGuessYear = getDaysInYear(year);
                if (dayOfYear < daysInGuessYear)
                    break;

                year++;
                dayOfYear -= daysInGuessYear;
            }
        }

        /// <summary>
        /// Calculates the year, month and day of the month in UTC from the given timestamp
        /// and writes them to the <paramref name="year"/>, <paramref name="month"/> and
        /// <paramref name="day"/> parameters. The month and day are both zero-based.
        /// </summary>
        ///
        /// <param name="timestamp">The timestamp value as represented in the  class.</param>
        /// <param name="year">The year.</param>
        /// <param name="month">The month.</param>
        /// <param name="day">The day of the month. This is zero-based, i.e. 0 is the first day of the
        /// month.</param>
        public static void getDayMonthYearFromTimestamp(long timestamp, out int year, out int month, out int day) {
            getYearAndDayFromTimestamp(timestamp, out year, out int dayOfYear);

            if (dayOfYear >= 59 && isLeapYear(year)) {
                month = s_monthLookupTable[dayOfYear - 1];
                day = dayOfYear - s_monthOffsets[month] - ((month >= 2) ? 1 : 0);
            }
            else {
                month = s_monthLookupTable[dayOfYear];
                day = dayOfYear - s_monthOffsets[month];
            }
        }

        /// <summary>
        /// Returns the offset of the current local time zone in milliseconds, assuming that
        /// daylight savings is not in effect.
        /// </summary>
        public static int localTimezoneOffset => s_baseTimezoneOffset;

        /// <summary>
        /// Returns the daylight savings rule that is applicable to the given local time.
        /// </summary>
        /// <param name="localTimestamp">A timestamp in the local standard time.</param>
        /// <returns>A <see cref="DSTRule"/> representing the rule applicable to
        /// the time represented by <paramref name="localTimestamp"/>, or null if
        /// no daylight savings rule applies.</returns>
        public static DSTRule getDaylightSavingsRule(long localTimestamp) {
            if (s_daylightRules == null)
                return null;

            int low = 0, high = s_daylightRules.Length;
            int days = (int)(localTimestamp / MS_PER_DAY);

            // Find the appropriate rule for the given date value using a binary search, as the rule
            // set array is sorted in increasing order of applicable date range.
            while (low != high) {
                int mid = low + ((high - low) >> 1);
                var rule = s_daylightRules[mid];

                if (days < rule.startDateStamp)
                    high = mid;
                else if (days > rule.endDateStamp)
                    low = mid + 1;
                else
                    return rule;
            }

            return null;
        }

        /// <summary>
        /// Converts a timestamp in UTC to local time.
        /// </summary>
        /// <param name="utcTimestamp">The timestamp in UTC to convert to local time.</param>
        /// <returns>The timestamp in local time.</returns>
        public static long universalTimestampToLocal(long utcTimestamp) {
            long localTimestampNoDST = utcTimestamp + localTimezoneOffset;

            var rule = getDaylightSavingsRule(localTimestampNoDST);
            if (rule == null)
                return localTimestampNoDST;

            getYearAndDayFromTimestamp(localTimestampNoDST, out int year, out _);

            long startDST = rule.daylightStart.getTimestampForYear(year);
            long endDST = rule.daylightEnd.getTimestampForYear(year);

            // Check if daylight saving time applies.
            // There are two possible cases: The start time for the year is less than the end time
            // (this is the case for Northern hemisphere time zones), in which case check if the
            // date value in local (standard) time is within that range.
            // Or the end time may be greater than the start time (this happens in time zones in the
            // Southern hemisphere), in which case daylight time is in effect only if the date value in
            // local standard time is either less than the end value or greater than the start value.
            if ((startDST < endDST)
                ? localTimestampNoDST >= startDST && localTimestampNoDST < endDST - rule.delta
                : localTimestampNoDST >= startDST || localTimestampNoDST < endDST - rule.delta)
            {
                return localTimestampNoDST + rule.delta;
            }

            return localTimestampNoDST;
        }

        /// <summary>
        /// Converts a timestamp given in local time to UTC.
        /// </summary>
        /// <param name="localTimestamp">The timestamp to convert to UTC.</param>
        /// <returns>The timestamp in UTC.</returns>
        ///
        /// <remarks>
        /// In time zones with DST, an invalid time is considered to be equivalent to the time ahead
        /// by the daylight saving offset (which is usually one hour, but can be different in a few
        /// time zones). An ambiguous time (a time which corresponds to two UTC times, which occurs
        /// during a backward transition) is considered to be the time that occurs after the
        /// transition.
        /// </remarks>
        public static long localTimestampToUniversal(long localTimestamp) {
            // Note that getDaylightSavingsRule accepts a timestamp in standard time, but here it is uncertain
            // whether the timestamp is in standard time or daylight savings time. However,
            // getDaylightSavingsRule should work with values in daylight saving time as well except in the
            // rare case where addition of the daylight saving offset results in the date overflowing
            // into the next day and resulting in a different rule being chosen. This is usually safe
            // as long as ambiguous times (which have more than one corresponding time in UTC)
            // are considered as standard times, and should give the desired result in all but the most
            // exceptional circumstances in a few time zones.

            long utcTimestampNoDST = localTimestamp - s_baseTimezoneOffset;

            var rule = getDaylightSavingsRule(localTimestamp);
            if (rule == null)
                return utcTimestampNoDST;

            getYearAndDayFromTimestamp(localTimestamp, out int year, out _);

            long startDST = rule.daylightStart.getTimestampForYear(year);
            long endDST = rule.daylightEnd.getTimestampForYear(year);

            // Here, the lower limit of the daylight interval is taken as start + delta (so that
            // delta is not subtracted from an invalid time, which must be considered to be equivalent to
            // the time that is ahead by the delta value) and the upper limit is taken to be the end time
            // in standard time (so that ambiguous times correspond to the standard time).
            if ((startDST < endDST)
                ? localTimestamp >= startDST + rule.delta && localTimestamp < endDST - rule.delta
                : localTimestamp >= startDST + rule.delta || localTimestamp < endDST - rule.delta)
            {
                return utcTimestampNoDST - rule.delta;
            }

            return utcTimestampNoDST;
        }

        /// <summary>
        /// Creates a timestamp from the given components.
        /// </summary>
        ///
        /// <param name="year">The year.</param>
        /// <param name="month">The month (zero-based).</param>
        /// <param name="day">The day of the month (zero-based).</param>
        /// <param name="hour">The hour.</param>
        /// <param name="min">The minute.</param>
        /// <param name="sec">The second.</param>
        /// <param name="ms">The millisecond.</param>
        /// <param name="isLocal">True if the date parameters are given in local time, false if they
        /// are given in UTC.</param>
        ///
        /// <returns>The created timestamp.</returns>
        public static long createTimestamp(
            int year, int month, int day, long hour, long min, long sec, long ms, bool isLocal)
        {
            adjustMonthAndYear(ref year, ref month);

            int days = getYearStartDaysFromZero(year) + getMonthStartDayOfYear(year, month) + day;
            long value = (long)days * MS_PER_DAY + (hour * MS_PER_HOUR + min * MS_PER_MIN + sec * MS_PER_SEC + ms);

            return isLocal ? localTimestampToUniversal(value) : value;
        }

        /// <summary>
        /// Creates the daylight savings rules from the current local time zone.
        /// </summary>
        /// <returns>An array of <see cref="DSTRule"/> instances.</returns>
        private static DSTRule[] _importRulesFromCurrentTimeZone() {
            var tzInfo = TimeZoneInfo.Local;
            if (!tzInfo.SupportsDaylightSavingTime)
                return null;

            var rulesFromTimeZone = tzInfo.GetAdjustmentRules();
            if (rulesFromTimeZone.Length == 0)
                return null;

            Array.Sort(rulesFromTimeZone, (r1, r2) => r1.DateStart.CompareTo(r2.DateStart));

            var rules = new DynamicArray<DSTRule>();

            for (int i = 0; i < rulesFromTimeZone.Length; i++) {
                TimeZoneInfo.AdjustmentRule adjustmentRule = rulesFromTimeZone[i];

                // Since subtracting two DateTime objects does not consider their kinds, the zero
                // unix time in its DateTime representation can be subtracted from the DateStart and
                // DateEnd values of the adjustment rule (which are usually of the unspecified kind)
                // to give the required offsets.

                const int unixZeroDays = (int)(UNIX_ZERO_TIMESTAMP / MS_PER_DAY);
                int ruleStart = (int)Math.Floor((adjustmentRule.DateStart - unixZeroAsDateTime).TotalDays) + unixZeroDays;
                int ruleEnd = (int)Math.Floor((adjustmentRule.DateEnd - unixZeroAsDateTime).TotalDays) + unixZeroDays;

                // Check for two special cases. In one case, the starting value of the rule's accepted range
                // is the minimum possible date, and in this case the rule should be considered for all dates
                // beyond it as well (upto the minimum possible value supported by the AS3 Date class).
                // In the other case, the end value of the accepted range for the rule is the maximum possible,
                // and such rules are considered for all dates beyond that date as well.

                if (adjustmentRule.DateStart == DateTime.MinValue)
                    ruleStart = Int32.MinValue;
                if (adjustmentRule.DateEnd == DateTime.MaxValue)
                    ruleEnd = Int32.MaxValue;

                long delta = (long)adjustmentRule.DaylightDelta.TotalMilliseconds;

                DSTTransition createTransition(TimeZoneInfo.TransitionTime transitionTime) {
                    int timeOfDay =
                        transitionTime.TimeOfDay.Hour * MS_PER_HOUR
                        + transitionTime.TimeOfDay.Minute * MS_PER_MIN
                        + transitionTime.TimeOfDay.Second * MS_PER_SEC
                        + transitionTime.TimeOfDay.Millisecond;

                    if (transitionTime.IsFixedDateRule) {
                        int offsetDay = getMonthStartDayOfYear(transitionTime.Month - 1, false) + transitionTime.Day - 1;
                        return DSTTransition.createAbsolute((long)offsetDay * MS_PER_DAY + timeOfDay);
                    }
                    else {
                        return DSTTransition.createWeekBased(
                            (byte)(transitionTime.Month - 1),
                            (byte)(transitionTime.Week - 1),
                            (byte)transitionTime.DayOfWeek,
                            timeOfDay
                        );
                    }
                }

                var daylightStart = createTransition(adjustmentRule.DaylightTransitionStart);
                var daylightEnd = createTransition(adjustmentRule.DaylightTransitionEnd);

                // Before adding a new rule to the rule set, check if it can be merged into the previous rule.
                if (i != 0) {
                    ref var previousRule = ref rules[rules.length - 1];
                    if (ruleStart - previousRule.endDateStamp <= 1
                        && delta == previousRule.delta
                        && daylightStart == previousRule.daylightStart
                        && daylightEnd == previousRule.daylightEnd)
                    {
                        previousRule = new DSTRule(previousRule.startDateStamp, ruleEnd, delta, daylightStart, daylightEnd);
                        continue;
                    }
                }

                rules.add(new DSTRule(ruleStart, ruleEnd, delta, daylightStart, daylightEnd));
            }

            return rules.toArray();
        }

        /// <summary>
        /// Represents a daylight savings time transition rule.
        /// </summary>
        public sealed class DSTRule {

            /// <summary>
            /// The date from when this rule comes into effect. This is represented as the difference in
            /// days from the zero timestamp value in the local standard time.
            /// </summary>
            public readonly int startDateStamp;

            /// <summary>
            /// The date after which this rule ceases to be in effect. This is represented as the difference
            /// in days from the zero timestamp value in the local standard time.
            /// </summary>
            public readonly int endDateStamp;

            /// <summary>
            /// The difference (in milliseconds) between the standard time and daylight savings time for
            /// this rule.
            /// </summary>
            public readonly long delta;

            /// <summary>
            /// A <see cref="DSTTransition"/> that represents the when the transition from standard to daylight
            /// time occues.
            /// </summary>
            public readonly DSTTransition daylightStart;

            /// <summary>
            /// A <see cref="DSTTransition"/> that represents the when the transition from daylight to standard
            /// time occues.
            /// </summary>
            public readonly DSTTransition daylightEnd;

            public DSTRule(
                int startDateStamp, int endDateStamp, long delta, DSTTransition daylightStart, DSTTransition daylightEnd)
            {
                this.startDateStamp = startDateStamp;
                this.endDateStamp = endDateStamp;
                this.delta = delta;
                this.daylightStart = daylightStart;
                this.daylightEnd = daylightEnd;
            }

        }

        #pragma warning disable 0660, 0661

        /// <summary>
        /// Represents a transition rule for standard to daylight savings time or vice versa.
        /// </summary>
        public readonly struct DSTTransition {

        #pragma warning restore

            private const long WEEK_BASED_RULE_BIT = unchecked((long)0x8000000000000000L);

            private readonly long m_data;
            private DSTTransition(long data) => m_data = data;

            /// <summary>
            /// Creates a transition rule based on an absolute time.
            /// </summary>
            /// <param name="nonLeapYearOffset">The offset in milliseconds from the beginning of a non-leap
            /// year at which the transition occurs.</param>
            /// <returns>A <see cref="DSTTransition"/> representing the transition.</returns>
            public static DSTTransition createAbsolute(long nonLeapYearOffset) => new DSTTransition(nonLeapYearOffset);

            /// <summary>
            /// Creates a transition rule based on an occurence of a particular day of the week in a month.
            /// </summary>
            /// <param name="month">The month (zero-based).</param>
            /// <param name="weekOfMonth">The occurence of the given day of the week in the month
            /// (zero-based). A value of 4 indicates that the transition occurs on the last occurence
            /// of the week day in the month.</param>
            /// <param name="dayOfWeek">The day of the week (zero-based).</param>
            /// <param name="timeOfDay">The time of the day (in milliseconds).</param>
            /// <returns>A <see cref="DSTTransition"/> representing the transition.</returns>
            public static DSTTransition createWeekBased(byte month, byte weekOfMonth, byte dayOfWeek, int timeOfDay) {
                return new DSTTransition(
                    (long)(month | (weekOfMonth << 8) | (dayOfWeek << 16))
                    | ((long)timeOfDay << 24)
                    | WEEK_BASED_RULE_BIT
                );
            }

            /// <summary>
            /// Returns the timestamp that represents the time at which the transition occurs
            /// in the given year.
            /// </summary>
            /// <param name="year">The year.</param>
            /// <returns>The timestamp that represents the time at which the transition occurs
            /// in the given year.</returns>
            public long getTimestampForYear(int year) {
                long timeFromYearStart;

                if ((m_data & WEEK_BASED_RULE_BIT) == 0) {
                    // For leap years, the leap day must be added only for offsets that cross 29 February
                    // of the year.
                    timeFromYearStart = m_data;
                    if (isLeapYear(year) && timeFromYearStart >= 59L * MS_PER_DAY)
                        timeFromYearStart += MS_PER_DAY;
                }
                else {
                    // For week-based rules.
                    int month = (byte)m_data;
                    int weekOfMonth = (byte)(m_data >> 8);
                    int dayOfWeek = (byte)(m_data >> 16);
                    int timeOfDay = (int)(m_data >> 24);

                    bool isLeap = isLeapYear(year);
                    int monthOffset = getMonthStartDayOfYear(month, isLeap);
                    int monthStartWeekDay = (getYearBeginWeekDay(year) + monthOffset) % 7;

                    int weekDayOffset = 0;
                    if (monthStartWeekDay < dayOfWeek)
                        weekDayOffset = dayOfWeek - monthStartWeekDay;
                    else if (monthStartWeekDay > dayOfWeek)
                        weekDayOffset = 7 - monthStartWeekDay + dayOfWeek;

                    if (weekOfMonth == 4) {
                        // A weekOfMonth of 4 is a special value, which indicates the last occurence
                        // (not necessarily the fifth) of the given day of the week in the month.
                        int nextMonthOffset = (month == 11)
                            ? (isLeap ? 366 : 365)
                            : getMonthStartDayOfYear(month + 1, isLeap);

                        weekOfMonth = (nextMonthOffset - monthOffset - weekDayOffset - 1) / 7;
                    }

                    timeFromYearStart = (long)(monthOffset + weekDayOffset + weekOfMonth * 7) * MS_PER_DAY + timeOfDay;
                }

                return timeFromYearStart + (long)getYearStartDaysFromZero(year) * MS_PER_DAY;
            }

            /// <summary>
            /// Returns a value indicating whether two <see cref="DSTTransition"/> instances are equal.
            /// </summary>
            /// <param name="x">The first <see cref="DSTTransition"/> instance.</param>
            /// <param name="y">The second <see cref="DSTTransition"/> instance.</param>
            /// <returns>True if <paramref name="x"/> is equal to <paramref name="y"/>, otherwise
            /// false.</returns>
            public static bool operator ==(DSTTransition x, DSTTransition y) => x.m_data == y.m_data;

            /// <summary>
            /// Returns a value indicating whether two <see cref="DSTTransition"/> instances are not equal.
            /// </summary>
            /// <param name="x">The first <see cref="DSTTransition"/> instance.</param>
            /// <param name="y">The second <see cref="DSTTransition"/> instance.</param>
            /// <returns>True if <paramref name="x"/> is not equal to <paramref name="y"/>, otherwise
            /// false.</returns>
            public static bool operator !=(DSTTransition x, DSTTransition y) => x.m_data != y.m_data;

        }

    }

}
