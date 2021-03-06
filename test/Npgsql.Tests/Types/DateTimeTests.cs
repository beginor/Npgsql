﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace Npgsql.Tests.Types
{
    /// <summary>
    /// Tests on PostgreSQL date/time types
    /// </summary>
    /// <remarks>
    /// http://www.postgresql.org/docs/current/static/datatype-datetime.html
    /// </remarks>
    class DateTimeTests : TestBase
    {
        #region Date

        [Test]
        public void Date()
        {
            var dateTime = new DateTime(2002, 3, 4, 0, 0, 0, 0, DateTimeKind.Unspecified);
            var npgsqlDate = new NpgsqlDate(dateTime);

            using (var cmd = new NpgsqlCommand("SELECT @p1, @p2, @p3", Conn))
            {
                var p1 = new NpgsqlParameter("p1", NpgsqlDbType.Date);
                var p2 = new NpgsqlParameter("p2", DbType.Date);
                var p3 = new NpgsqlParameter {ParameterName = "p3", Value = npgsqlDate};
                Assert.That(p3.NpgsqlDbType, Is.EqualTo(NpgsqlDbType.Date));
                Assert.That(p3.DbType, Is.EqualTo(DbType.Date));
                cmd.Parameters.Add(p1);
                cmd.Parameters.Add(p2);
                cmd.Parameters.Add(p3);
                p1.Value = p2.Value = npgsqlDate;
                using (var reader = cmd.ExecuteReader())
                {
                    reader.Read();

                    for (var i = 0; i < cmd.Parameters.Count; i++)
                    {
                        // Regular type (DateTime)
                        Assert.That(reader.GetFieldType(i), Is.EqualTo(typeof (DateTime)));
                        Assert.That(reader.GetDateTime(i), Is.EqualTo(dateTime));
                        Assert.That(reader.GetFieldValue<DateTime>(i), Is.EqualTo(dateTime));
                        Assert.That(reader[i], Is.EqualTo(dateTime));
                        Assert.That(reader.GetValue(i), Is.EqualTo(dateTime));

                        // Provider-specific type (NpgsqlDate)
                        Assert.That(reader.GetDate(i), Is.EqualTo(npgsqlDate));
                        Assert.That(reader.GetProviderSpecificFieldType(i), Is.EqualTo(typeof (NpgsqlDate)));
                        Assert.That(reader.GetProviderSpecificValue(i), Is.EqualTo(npgsqlDate));
                        Assert.That(reader.GetFieldValue<NpgsqlDate>(i), Is.EqualTo(npgsqlDate));
                    }
                }
            }
        }

        static readonly TestCaseData[] DateSpecialCases = {
            new TestCaseData(NpgsqlDate.Infinity).SetName("Infinity"),
            new TestCaseData(NpgsqlDate.NegativeInfinity).SetName("NegativeInfinity"),
            new TestCaseData(new NpgsqlDate(-5, 3, 3)).SetName("BC"),
        };

        [Test, TestCaseSource("DateSpecialCases")]
        public void DateSpecial(NpgsqlDate value)
        {
            using (var cmd = new NpgsqlCommand("SELECT @p", Conn)) {
                cmd.Parameters.Add(new NpgsqlParameter { ParameterName = "p", Value = value });
                using (var reader = cmd.ExecuteReader()) {
                    reader.Read();
                    Assert.That(reader.GetProviderSpecificValue(0), Is.EqualTo(value));
                    Assert.That(() => reader.GetDateTime(0), Throws.Exception);
                }
                Assert.That(ExecuteScalar("SELECT 1"), Is.EqualTo(1));
            }
        }

        [Test, Description("Makes sure that when ConvertInfinityDateTime is true, infinity values are properly converted")]
        public void DateConvertInfinity()
        {
            using (var conn = new NpgsqlConnection(ConnectionString + ";ConvertInfinityDateTime=true"))
            {
                conn.Open();

                using (var cmd = new NpgsqlCommand("SELECT @p1, @p2", conn)) {
                    cmd.Parameters.AddWithValue("p1", NpgsqlDbType.Date, DateTime.MaxValue);
                    cmd.Parameters.AddWithValue("p2", NpgsqlDbType.Date, DateTime.MinValue);
                    using (var reader = cmd.ExecuteReader()) {
                        reader.Read();
                        Assert.That(reader.GetFieldValue<NpgsqlDate>(0), Is.EqualTo(NpgsqlDate.Infinity));
                        Assert.That(reader.GetFieldValue<NpgsqlDate>(1), Is.EqualTo(NpgsqlDate.NegativeInfinity));
                        Assert.That(reader.GetDateTime(0), Is.EqualTo(DateTime.MaxValue));
                        Assert.That(reader.GetDateTime(1), Is.EqualTo(DateTime.MinValue));
                    }
                }
            }
        }

        #endregion

        #region Time

        [Test]
        public void Time()
        {
            var expected = new TimeSpan(0, 10, 45, 34, 500);

            using (var cmd = new NpgsqlCommand("SELECT @p1, @p2, @p3", Conn))
            {
                var p1 = new NpgsqlParameter("p1", NpgsqlDbType.Time);
                var p2 = new NpgsqlParameter("p2", DbType.Time);
                var p3 = new NpgsqlParameter("p3", expected);
                cmd.Parameters.Add(p1);
                cmd.Parameters.Add(p2);
                cmd.Parameters.Add(p3);
                p1.Value = p2.Value = expected;
                using (var reader = cmd.ExecuteReader())
                {
                    reader.Read();

                    for (var i = 0; i < cmd.Parameters.Count; i++)
                    {
                        Assert.That(reader.GetFieldType(i), Is.EqualTo(typeof (TimeSpan)));
                        Assert.That(reader.GetTimeSpan(i), Is.EqualTo(expected));
                        Assert.That(reader.GetFieldValue<TimeSpan>(i), Is.EqualTo(expected));
                        Assert.That(reader[i], Is.EqualTo(expected));
                        Assert.That(reader.GetValue(i), Is.EqualTo(expected));
                    }
                }
            }
        }

        #endregion

        #region Time with timezone

        [Test]
        public void TimeTz()
        {
            var tzOffset = TimeZoneInfo.Local.BaseUtcOffset;
            if (tzOffset == TimeSpan.Zero)
                TestUtil.Inconclusive("Test cannot run when machine timezone is UTC");

            // Note that the date component of the below is ignored
            var dto = new DateTimeOffset(5, 5, 5, 13, 3, 45, 510, tzOffset);
            var dtUtc = new DateTime(dto.Year, dto.Month, dto.Day, dto.Hour, dto.Minute, dto.Second, dto.Millisecond, DateTimeKind.Utc) - tzOffset;
            var dtLocal = new DateTime(dto.Year, dto.Month, dto.Day, dto.Hour, dto.Minute, dto.Second, dto.Millisecond, DateTimeKind.Local);
            var dtUnspecified = new DateTime(dto.Year, dto.Month, dto.Day, dto.Hour, dto.Minute, dto.Second, dto.Millisecond, DateTimeKind.Unspecified);
            var ts = dto.TimeOfDay;

            using (var cmd = new NpgsqlCommand("SELECT @p1, @p2, @p3, @p4, @p5", Conn))
            {
                cmd.Parameters.AddWithValue("p1", NpgsqlDbType.TimeTZ, dto);
                cmd.Parameters.AddWithValue("p2", NpgsqlDbType.TimeTZ, dtUtc);
                cmd.Parameters.AddWithValue("p3", NpgsqlDbType.TimeTZ, dtLocal);
                cmd.Parameters.AddWithValue("p4", NpgsqlDbType.TimeTZ, dtUnspecified);
                cmd.Parameters.AddWithValue("p5", NpgsqlDbType.TimeTZ, ts);
                Assert.That(cmd.Parameters.All(p => p.DbType == DbType.Object));

                using (var reader = cmd.ExecuteReader())
                {
                    reader.Read();

                    for (var i = 0; i < cmd.Parameters.Count; i++)
                    {
                        Assert.That(reader.GetFieldType(i), Is.EqualTo(typeof(DateTimeOffset)));

                        Assert.That(reader.GetFieldValue<DateTimeOffset>(i), Is.EqualTo(new DateTimeOffset(1, 1, 1, dto.Hour, dto.Minute, dto.Second, dto.Millisecond, dto.Offset)));
                        Assert.That(reader.GetFieldType(i), Is.EqualTo(typeof(DateTimeOffset)));
                        Assert.That(reader.GetFieldValue<DateTime>(i).Kind, Is.EqualTo(DateTimeKind.Local));
                        Assert.That(reader.GetFieldValue<DateTime>(i), Is.EqualTo(reader.GetFieldValue<DateTimeOffset>(i).LocalDateTime));
                        Assert.That(reader.GetFieldValue<TimeSpan>(i), Is.EqualTo(reader.GetFieldValue<DateTimeOffset>(i).LocalDateTime.TimeOfDay));
                    }
                }
            }
        }

        #endregion

        #region Timestamp

        static readonly TestCaseData[] TimeStampCases = {
            new TestCaseData(new DateTime(1998, 4, 12, 13, 26, 38)).SetName("Pre2000"),
            new TestCaseData(new DateTime(2015, 1, 27, 8, 45, 12, 345)).SetName("Post2000"),
            new TestCaseData(new DateTime(2013, 7, 25)).SetName("DateOnly"),
        };

        [Test, TestCaseSource("TimeStampCases")]
        public void Timestamp(DateTime dateTime)
        {
            var npgsqlTimeStamp = new NpgsqlDateTime(dateTime.Ticks);
            var offset = TimeSpan.FromHours(2);
            var dateTimeOffset = new DateTimeOffset(dateTime, offset);

            using (var cmd = new NpgsqlCommand("SELECT @p1, @p2, @p3, @p4, @p5, @p6", Conn))
            {
                var p1 = new NpgsqlParameter("p1", NpgsqlDbType.Timestamp);
                var p2 = new NpgsqlParameter("p2", DbType.DateTime);
                var p3 = new NpgsqlParameter("p3", DbType.DateTime2);
                var p4 = new NpgsqlParameter { ParameterName = "p4", Value = npgsqlTimeStamp };
                var p5 = new NpgsqlParameter { ParameterName = "p5", Value = dateTime };
                var p6 = new NpgsqlParameter("p6", NpgsqlDbType.Timestamp);
                Assert.That(p4.NpgsqlDbType, Is.EqualTo(NpgsqlDbType.Timestamp));
                Assert.That(p4.DbType, Is.EqualTo(DbType.DateTime));
                Assert.That(p5.NpgsqlDbType, Is.EqualTo(NpgsqlDbType.Timestamp));
                Assert.That(p5.DbType, Is.EqualTo(DbType.DateTime));
                cmd.Parameters.Add(p1);
                cmd.Parameters.Add(p2);
                cmd.Parameters.Add(p3);
                cmd.Parameters.Add(p4);
                cmd.Parameters.Add(p5);
                cmd.Parameters.Add(p6);
                p1.Value = p2.Value = p3.Value = npgsqlTimeStamp;
                p6.Value = dateTimeOffset;
                using (var reader = cmd.ExecuteReader())
                {
                    reader.Read();

                    for (var i = 0; i < cmd.Parameters.Count; i++)
                    {
                        // Regular type (DateTime)
                        Assert.That(reader.GetFieldType(i), Is.EqualTo(typeof (DateTime)));
                        Assert.That(reader.GetDateTime(i), Is.EqualTo(dateTime));
                        Assert.That(reader.GetDateTime(i).Kind, Is.EqualTo(DateTimeKind.Unspecified));
                        Assert.That(reader.GetFieldValue<DateTime>(i), Is.EqualTo(dateTime));
                        Assert.That(reader[i], Is.EqualTo(dateTime));
                        Assert.That(reader.GetValue(i), Is.EqualTo(dateTime));

                        // Provider-specific type (NpgsqlTimeStamp)
                        Assert.That(reader.GetTimeStamp(i), Is.EqualTo(npgsqlTimeStamp));
                        Assert.That(reader.GetProviderSpecificFieldType(i), Is.EqualTo(typeof (NpgsqlDateTime)));
                        Assert.That(reader.GetProviderSpecificValue(i), Is.EqualTo(npgsqlTimeStamp));
                        Assert.That(reader.GetFieldValue<NpgsqlDateTime>(i), Is.EqualTo(npgsqlTimeStamp));

                        // DateTimeOffset
                        Assert.That(() => reader.GetFieldValue<DateTimeOffset>(i), Throws.Exception);
                    }
                }
            }
        }

        static readonly TestCaseData[] TimeStampSpecialCases = {
            new TestCaseData(NpgsqlDateTime.Infinity).SetName("Infinity"),
            new TestCaseData(NpgsqlDateTime.NegativeInfinity).SetName("NegativeInfinity"),
            new TestCaseData(new NpgsqlDateTime(-5, 3, 3, 1, 0, 0)).SetName("BC"),
        };

        [Test, TestCaseSource("TimeStampSpecialCases")]
        public void TimeStampSpecial(NpgsqlDateTime value)
        {
            using (var cmd = new NpgsqlCommand("SELECT @p", Conn)) {
                cmd.Parameters.Add(new NpgsqlParameter { ParameterName = "p", Value = value });
                using (var reader = cmd.ExecuteReader()) {
                    reader.Read();
                    Assert.That(reader.GetProviderSpecificValue(0), Is.EqualTo(value));
                    Assert.That(() => reader.GetDateTime(0), Throws.Exception);
                }
                Assert.That(ExecuteScalar("SELECT 1"), Is.EqualTo(1));
            }
        }

        [Test, Description("Makes sure that when ConvertInfinityDateTime is true, infinity values are properly converted")]
        public void TimeStampConvertInfinity()
        {
            using (var conn = new NpgsqlConnection(ConnectionString + ";ConvertInfinityDateTime=true"))
            {
                conn.Open();

                using (var cmd = new NpgsqlCommand("SELECT @p1, @p2", conn))
                {
                    cmd.Parameters.AddWithValue("p1", NpgsqlDbType.Timestamp, DateTime.MaxValue);
                    cmd.Parameters.AddWithValue("p2", NpgsqlDbType.Timestamp, DateTime.MinValue);
                    using (var reader = cmd.ExecuteReader())
                    {
                        reader.Read();
                        Assert.That(reader.GetFieldValue<NpgsqlDateTime>(0), Is.EqualTo(NpgsqlDateTime.Infinity));
                        Assert.That(reader.GetFieldValue<NpgsqlDateTime>(1), Is.EqualTo(NpgsqlDateTime.NegativeInfinity));
                        Assert.That(reader.GetDateTime(0), Is.EqualTo(DateTime.MaxValue));
                        Assert.That(reader.GetDateTime(1), Is.EqualTo(DateTime.MinValue));
                    }
                }
            }
        }

        #endregion

        #region Timestamp with timezone

        [Test]
        public void TimestampTz()
        {
            var tzOffset = TimeZoneInfo.Local.BaseUtcOffset;
            if (tzOffset == TimeSpan.Zero)
                TestUtil.Inconclusive("Test cannot run when machine timezone is UTC");

            var dateTimeUtc = new DateTime(2015, 1, 27, 8, 45, 12, 345, DateTimeKind.Utc);
            var dateTimeLocal = dateTimeUtc.ToLocalTime();
            var dateTimeUnspecified = new DateTime(dateTimeUtc.Ticks, DateTimeKind.Unspecified);

            var nDateTimeUtc = new NpgsqlDateTime(dateTimeUtc);
            var nDateTimeLocal = nDateTimeUtc.ToLocalTime();
            var nDateTimeUnspecified = new NpgsqlDateTime(nDateTimeUtc.Ticks, DateTimeKind.Unspecified);

            var dateTimeOffset = new DateTimeOffset(dateTimeLocal, tzOffset);

            using (var cmd = new NpgsqlCommand("SELECT @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9", Conn))
            {
                cmd.Parameters.AddWithValue("p1", NpgsqlDbType.TimestampTZ, dateTimeUtc);
                cmd.Parameters.AddWithValue("p2", NpgsqlDbType.TimestampTZ, dateTimeLocal);
                cmd.Parameters.AddWithValue("p3", NpgsqlDbType.TimestampTZ, dateTimeUnspecified);
                cmd.Parameters.AddWithValue("p4", NpgsqlDbType.TimestampTZ, nDateTimeUtc);
                cmd.Parameters.AddWithValue("p5", NpgsqlDbType.TimestampTZ, nDateTimeLocal);
                cmd.Parameters.AddWithValue("p6", NpgsqlDbType.TimestampTZ, nDateTimeUnspecified);
                cmd.Parameters.AddWithValue("p7", dateTimeUtc);
                cmd.Parameters.AddWithValue("p8", nDateTimeUtc);
                cmd.Parameters.AddWithValue("p9", dateTimeOffset);

                using (var reader = cmd.ExecuteReader())
                {
                    reader.Read();

                    for (var i = 0; i < cmd.Parameters.Count; i++)
                    {
                        // Regular type (DateTime)
                        Assert.That(reader.GetFieldType(i), Is.EqualTo(typeof(DateTime)));
                        Assert.That(reader.GetDateTime(i), Is.EqualTo(dateTimeUtc));
                        Assert.That(reader.GetFieldValue<DateTime>(i).Kind, Is.EqualTo(DateTimeKind.Utc));
                        Assert.That(reader[i], Is.EqualTo(dateTimeUtc));
                        Assert.That(reader.GetValue(i), Is.EqualTo(dateTimeUtc));

                        // Provider-specific type (NpgsqlDateTime)
                        Assert.That(reader.GetTimeStamp(i), Is.EqualTo(nDateTimeUtc));
                        Assert.That(reader.GetProviderSpecificFieldType(i), Is.EqualTo(typeof(NpgsqlDateTime)));
                        Assert.That(reader.GetProviderSpecificValue(i), Is.EqualTo(nDateTimeUtc));
                        Assert.That(reader.GetFieldValue<NpgsqlDateTime>(i), Is.EqualTo(nDateTimeUtc));

                        // DateTimeOffset
                        Assert.That(reader.GetFieldValue<DateTimeOffset>(i), Is.EqualTo(dateTimeOffset.ToUniversalTime()));
                    }
                }
            }
        }

        #endregion

        #region Interval

        [Test]
        public void Interval()
        {
            var expectedNpgsqlInterval = new NpgsqlTimeSpan(1, 2, 3, 4, 5);
            var expectedTimeSpan = new TimeSpan(1, 2, 3, 4, 5);

            using (var cmd = new NpgsqlCommand("SELECT @p1, @p2", Conn))
            {
                var p1 = new NpgsqlParameter("p1", NpgsqlDbType.Interval);
                var p2 = new NpgsqlParameter("p2", expectedNpgsqlInterval);
                Assert.That(p2.NpgsqlDbType, Is.EqualTo(NpgsqlDbType.Interval));
                Assert.That(p2.DbType, Is.EqualTo(DbType.Object));
                cmd.Parameters.Add(p1);
                cmd.Parameters.Add(p2);
                p1.Value = expectedNpgsqlInterval;

                using (var reader = cmd.ExecuteReader())
                {
                    reader.Read();

                    // Regular type (TimeSpan)
                    Assert.That(reader.GetFieldType(0), Is.EqualTo(typeof (TimeSpan)));
                    Assert.That(reader.GetTimeSpan(0), Is.EqualTo(expectedTimeSpan));
                    Assert.That(reader.GetFieldValue<TimeSpan>(0), Is.EqualTo(expectedTimeSpan));
                    Assert.That(reader[0], Is.EqualTo(expectedTimeSpan));
                    Assert.That(reader.GetValue(0), Is.EqualTo(expectedTimeSpan));

                    // Provider-specific type (NpgsqlInterval)
                    Assert.That(reader.GetInterval(0), Is.EqualTo(expectedNpgsqlInterval));
                    Assert.That(reader.GetProviderSpecificFieldType(0), Is.EqualTo(typeof (NpgsqlTimeSpan)));
                    Assert.That(reader.GetProviderSpecificValue(0), Is.EqualTo(expectedNpgsqlInterval));
                    Assert.That(reader.GetFieldValue<NpgsqlTimeSpan>(0), Is.EqualTo(expectedNpgsqlInterval));
                }
            }
        }

        #endregion

        public DateTimeTests(string backendVersion) : base(backendVersion) { }
    }
}
