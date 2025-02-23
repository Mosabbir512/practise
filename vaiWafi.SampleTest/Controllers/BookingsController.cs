using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using Wafi.SampleTest.Dtos;
using Wafi.SampleTest.Entities;

namespace Wafi.SampleTest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BookingsController : ControllerBase
    {
        private readonly WafiDbContext _context;

        public BookingsController(WafiDbContext context)
        {
            _context = context;
        }

        // GET: api/Bookings
        [HttpPost("BookingCalendar")]
        public async Task<IEnumerable<BookingCalendarDto>> GetCalendarBookings([FromBody] BookingFilterDto input)
        {
            var bookings = await _context.Bookings
                .Include(b => b.Car) // Ensure Car is loaded
                .Where(b => b.CarId == input.CarId &&
                            (b.BookingDate >= input.StartBookingDate && b.BookingDate <= input.EndBookingDate ||
                             (b.RepeatOption != RepeatOption.DoesNotRepeat && b.EndRepeatDate >= input.StartBookingDate)))
                .ToListAsync();

            var calendarBookings = new List<BookingCalendarDto>();

            foreach (var booking in bookings)
            {
                var currentDate = booking.BookingDate;
                var endRepeatDate = booking.EndRepeatDate ?? booking.BookingDate; // If no repeat, use booking date

                // Ensure we only add relevant bookings once per unique date
                var addedDates = new HashSet<DateOnly>();

                while (currentDate <= endRepeatDate && currentDate <= input.EndBookingDate)
                {
                    if (currentDate >= input.StartBookingDate && !addedDates.Contains(currentDate))
                    {
                        calendarBookings.Add(new BookingCalendarDto
                        {
                            BookingDate = currentDate,
                            CarModel = booking.Car?.Model ?? "Unknown", // Prevent null reference
                            StartTime = booking.StartTime,
                            EndTime = booking.EndTime
                        });

                        addedDates.Add(currentDate);
                    }

                    if (booking.RepeatOption == RepeatOption.DoesNotRepeat)
                        break; // 🚀 Stop the loop for non-repeating bookings!

                    // Move to the next date based on the repeat option
                    currentDate = booking.RepeatOption switch
                    {
                        RepeatOption.Daily => currentDate.AddDays(1),
                        RepeatOption.Weekly => currentDate.AddDays(7),
                        _ => endRepeatDate.AddDays(1) // Ensure loop stops at the last valid repeat date
                    };
                }
            }

            return calendarBookings;
        }


        // POST: api/Bookings
        [HttpPost("Booking")]
      public async Task<ActionResult<CreateUpdateBookingDto>> PostBooking([FromBody] CreateUpdateBookingDto bookingDto)
    {
        // Validate if the booking already exists or there are conflicts
        var conflicts = await _context.Bookings
            .Where(b => b.CarId == bookingDto.CarId &&
                        b.BookingDate == bookingDto.BookingDate &&
                        b.StartTime < bookingDto.EndTime &&
                        b.EndTime > bookingDto.StartTime)
            .AnyAsync();

        if (conflicts)
        {
            return BadRequest("Booking time conflicts with an existing booking.");
        }

        // Map the DTO to the entity
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingDate = bookingDto.BookingDate,
            StartTime = bookingDto.StartTime,
            EndTime = bookingDto.EndTime,
            RepeatOption = bookingDto.RepeatOption,
            EndRepeatDate = bookingDto.EndRepeatDate,
            DaysToRepeatOn = bookingDto.DaysToRepeatOn ?? 0,  // Default to 0 if null
            RequestedOn = DateTime.Now,
            CarId = bookingDto.CarId,
            // If you're returning full booking data, ensure the Car property is populated
            Car = await _context.Cars.FindAsync(bookingDto.CarId)
        };

        // Add to the database and save
        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        // Map back to the DTO if needed for the response
        var createdBookingDto = new CreateUpdateBookingDto
        {
            Id = booking.Id,
            BookingDate = booking.BookingDate,
            StartTime = booking.StartTime,
            EndTime = booking.EndTime,
            RepeatOption = booking.RepeatOption,
            EndRepeatDate = booking.EndRepeatDate,
            DaysToRepeatOn = booking.DaysToRepeatOn,
            RequestedOn = booking.RequestedOn,
            CarId = booking.CarId
        };

        return CreatedAtAction(nameof(PostBooking), new { id = createdBookingDto.Id }, createdBookingDto);
    }


        // GET: api/SeedData
        // For test purpose
        [HttpGet("SeedData")]
        public async Task<IEnumerable<BookingCalendarDto>> GetSeedData()
        {
            var cars = await _context.Cars.ToListAsync();

            if (!cars.Any())
            {
                cars = GetCars().ToList();
                await _context.Cars.AddRangeAsync(cars);
                await _context.SaveChangesAsync();
            }

            var bookings = await _context.Bookings.ToListAsync();

            if(!bookings.Any())
            {
                bookings = GetBookings().ToList();

                await _context.Bookings.AddRangeAsync(bookings);
                await _context.SaveChangesAsync();
            }

            var calendar = new Dictionary<DateOnly, List<Booking>>();

            foreach (var booking in bookings)
            {
                var currentDate = booking.BookingDate;
                while (currentDate <= (booking.EndRepeatDate ?? booking.BookingDate))
                {
                    if (!calendar.ContainsKey(currentDate))
                        calendar[currentDate] = new List<Booking>();

                    calendar[currentDate].Add(booking);

                    currentDate = booking.RepeatOption switch
                    {
                        RepeatOption.Daily => currentDate.AddDays(1),
                        RepeatOption.Weekly => currentDate.AddDays(7),
                        _ => booking.EndRepeatDate.HasValue ? booking.EndRepeatDate.Value.AddDays(1) : currentDate.AddDays(1)
                    };
                }
            }

            List<BookingCalendarDto> result = new List<BookingCalendarDto>();

            foreach (var item in calendar)
            {
                foreach(var booking in item.Value)
                {
                    result.Add(new BookingCalendarDto { BookingDate = booking.BookingDate, CarModel = booking.Car.Model, StartTime = booking.StartTime, EndTime = booking.EndTime });
                }
            }

            return result;
        }

        #region Sample Data

        private IList<Car> GetCars()
        {
            var cars = new List<Car>
            {
                new Car { Id = Guid.NewGuid(), Make = "Toyota", Model = "Corolla" },
                new Car { Id = Guid.NewGuid(), Make = "Honda", Model = "Civic" },
                new Car { Id = Guid.NewGuid(), Make = "Ford", Model = "Focus" }
            };

            return cars;
        }

        private IList<Booking> GetBookings()
        {
            var cars = GetCars();

            var bookings = new List<Booking>
            {
                new Booking { Id = Guid.NewGuid(), BookingDate = new DateOnly(2025, 2, 5), StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(12, 0, 0), RepeatOption = RepeatOption.DoesNotRepeat, RequestedOn = DateTime.Now, CarId = cars[0].Id, Car = cars[0] },
                new Booking { Id = Guid.NewGuid(), BookingDate = new DateOnly(2025, 2, 10), StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(16, 0, 0), RepeatOption = RepeatOption.Daily, EndRepeatDate = new DateOnly(2025, 2, 20), RequestedOn = DateTime.Now, CarId = cars[1].Id, Car = cars[1] },
                new Booking { Id = Guid.NewGuid(), BookingDate = new DateOnly(2025, 2, 15), StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 30, 0), RepeatOption = RepeatOption.Weekly, EndRepeatDate = new DateOnly(2025, 3, 31), RequestedOn = DateTime.Now, DaysToRepeatOn = DaysOfWeek.Monday, CarId = cars[2].Id,  Car = cars[2] },
                new Booking { Id = Guid.NewGuid(), BookingDate = new DateOnly(2025, 3, 1), StartTime = new TimeSpan(11, 0, 0), EndTime = new TimeSpan(13, 0, 0), RepeatOption = RepeatOption.DoesNotRepeat, RequestedOn = DateTime.Now, CarId = cars[0].Id, Car = cars[0] },
                new Booking { Id = Guid.NewGuid(), BookingDate = new DateOnly(2025, 3, 7), StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(10, 0, 0), RepeatOption = RepeatOption.Weekly, EndRepeatDate = new DateOnly(2025, 3, 28), RequestedOn = DateTime.Now, DaysToRepeatOn = DaysOfWeek.Friday, CarId = cars[1].Id, Car = cars[1] },
                new Booking { Id = Guid.NewGuid(), BookingDate = new DateOnly(2025, 3, 15), StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(17, 0, 0), RepeatOption = RepeatOption.Daily, EndRepeatDate = new DateOnly(2025, 3, 20), RequestedOn = DateTime.Now, CarId = cars[2].Id,  Car = cars[2] }
            };

            return bookings;
        }

            #endregion

        }
}
