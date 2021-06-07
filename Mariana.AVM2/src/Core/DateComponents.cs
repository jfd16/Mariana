using System;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A structure containing the components of a Date object returned by
    /// the <see cref="ASDate.getDateComponents"/> method.
    /// </summary>
    public readonly struct DateComponents {

        /// <summary>
        /// The year component.
        /// </summary>
        public readonly int year;

        /// <summary>
        /// The offset, in minutes, that must be added to a local time instant to obtain the
        /// corresponding time in UTC. This includes any daylight savings adjustments. If <see cref="isUTC"/>
        /// is true, the value of this field is zero.
        /// </summary>
        public readonly int timezoneOffset;

        /// <summary>
        /// The day of the year (zero-based).
        /// </summary>
        public readonly short dayOfYear;

        /// <summary>
        /// The millisecond component.
        /// </summary>
        public readonly short millisecond;

        /// <summary>
        /// The second component.
        /// </summary>
        public readonly byte second;

        /// <summary>
        /// The minute component.
        /// </summary>
        public readonly byte minute;

        /// <summary>
        /// The hour component.
        /// </summary>
        public readonly byte hour;

        /// <summary>
        /// The day of the month (one-based).
        /// </summary>
        public readonly byte day;

        /// <summary>
        /// The month component (zero-based).
        /// </summary>
        public readonly byte month;

        /// <summary>
        /// The day of the week (zero-based, with Sunday being the first day).
        /// </summary>
        public readonly byte dayOfWeek;

        /// <summary>
        /// True if this <see cref="DateComponents"/> instance represents a time in UTC, false if it
        /// represents a time in the local time zone.
        /// </summary>
        public readonly bool isUTC;

        /// <summary>
        /// True if this <see cref="DateComponents"/> instance represents a time in the local time zone
        /// that contains a daylight savings adjustment, otherwise false.
        /// </summary>
        public readonly bool isDaylightTime;

        internal DateComponents(
            int year,
            int month,
            int day,
            int hour,
            int minute,
            int second,
            int millisecond,
            int dayOfYear,
            int dayOfWeek,
            int timezoneOffset,
            bool isUTC,
            bool isDaylightTime
        ) {
            this.year = year;
            this.month = (byte)month;
            this.day = (byte)day;
            this.hour = (byte)hour;
            this.minute = (byte)minute;
            this.second = (byte)second;
            this.millisecond = (short)millisecond;
            this.dayOfYear = (short)dayOfYear;
            this.dayOfWeek = (byte)dayOfWeek;
            this.timezoneOffset = timezoneOffset;
            this.isUTC = isUTC;
            this.isDaylightTime = isDaylightTime;
        }

    }

}
