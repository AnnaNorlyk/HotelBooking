using HotelBooking.Core;
using HotelBooking.UnitTests.Fakes;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;


namespace HotelBooking.UnitTests
{
    public class BookingManagerTests
    {
        private IBookingManager bookingManager;
        IRepository<Booking> bookingRepository;

        public BookingManagerTests(){
            DateTime start = DateTime.Today.AddDays(10);
            DateTime end = DateTime.Today.AddDays(20);
            bookingRepository = new FakeBookingRepository(start, end);
            IRepository<Room> roomRepository = new FakeRoomRepository();
            bookingManager = new BookingManager(bookingRepository, roomRepository);
        }

        [Fact]
        public async Task FindAvailableRoom_StartDateNotInTheFuture_ThrowsArgumentException()
        {
            // Arrange
            DateTime date = DateTime.Today;

            // Act
            Task result() => bookingManager.FindAvailableRoom(date, date);

            // Assert
            await Assert.ThrowsAsync<ArgumentException>(result);
        }

        [Fact]
        public async Task FindAvailableRoom_RoomAvailable_RoomIdNotMinusOne()
        {
            // Arrange
            DateTime date = DateTime.Today.AddDays(1);
            // Act
            int roomId = await bookingManager.FindAvailableRoom(date, date);
            // Assert
            Assert.NotEqual(-1, roomId);
        }

        [Fact]
        public async Task FindAvailableRoom_RoomAvailable_ReturnsAvailableRoom()
        {
            // principle: "Tests should have strong assertions".

            // Arrange
            DateTime date = DateTime.Today.AddDays(1);
            
            // Act
            int roomId = await bookingManager.FindAvailableRoom(date, date);

            var bookingForReturnedRoomId = (await bookingRepository.GetAllAsync()).
                Where(b => b.RoomId == roomId
                           && b.StartDate <= date
                           && b.EndDate >= date
                           && b.IsActive);
            
            // Assert
            Assert.Empty(bookingForReturnedRoomId);
        }
        //---------------- Our tests -----------------

        // ============================================================
        // Data-driven test 
        // ============================================================

        public static IEnumerable<object[]> FindAvailableRoomCases()
        {

            yield return new object[]
            {
                "No bookings => returns first room",
                new List<Room> { new Room { Id = 1 }, new Room { Id = 2 } },
                new List<Booking>(),
                DateTime.Today.AddDays(1),
                DateTime.Today.AddDays(2),
                1
            };

            yield return new object[]
            {
                "Room1 overlaps => returns room2",
                new List<Room> { new Room { Id = 1 }, new Room { Id = 2 } },
                new List<Booking>
                {
                    new Booking
                    {
                        RoomId = 1,
                        IsActive = true,
                        StartDate = DateTime.Today.AddDays(3),
                        EndDate = DateTime.Today.AddDays(5)
                    }
                },
                DateTime.Today.AddDays(4),
                DateTime.Today.AddDays(4),
                2
            };

            yield return new object[]
            {
                "All rooms overlap => returns -1",
                new List<Room> { new Room { Id = 1 }, new Room { Id = 2 } },
                new List<Booking>
                {
                    new Booking
                    {
                        RoomId = 1,
                        IsActive = true,
                        StartDate = DateTime.Today.AddDays(1),
                        EndDate = DateTime.Today.AddDays(10)
                    },
                    new Booking
                    {
                        RoomId = 2,
                        IsActive = true,
                        StartDate = DateTime.Today.AddDays(1),
                        EndDate = DateTime.Today.AddDays(10)
                    }
                },
                DateTime.Today.AddDays(4),
                DateTime.Today.AddDays(4),
                -1
            };

            yield return new object[]
            {
                "Inactive booking ignored => room1 returned",
                new List<Room> { new Room { Id = 1 } },
                new List<Booking>
                {
                    new Booking
                    {
                        RoomId = 1,
                        IsActive = false,
                        StartDate = DateTime.Today.AddDays(1),
                        EndDate = DateTime.Today.AddDays(10)
                    }
                },
                DateTime.Today.AddDays(2),
                DateTime.Today.AddDays(3),
                1
            };
        }

        [Theory]
        [MemberData(nameof(FindAvailableRoomCases))]
        public async Task FindAvailableRoom_DataDriven_ReturnsExpectedRoomId(
            string _caseName,
            List<Room> rooms,
            List<Booking> bookings,
            DateTime start,
            DateTime end,
            int expectedRoomId)
        {
            // Arrange (mock repos to control data precisely)
            var bookingRepoMock = new Mock<IRepository<Booking>>();
            var roomRepoMock = new Mock<IRepository<Room>>();

            bookingRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(bookings);
            roomRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(rooms);

            var sut = new BookingManager(bookingRepoMock.Object, roomRepoMock.Object);

            // Act
            int roomId = await sut.FindAvailableRoom(start, end);

            // Assert
            Assert.Equal(expectedRoomId, roomId);

            // Strong assertion: returned room must not have any ACTIVE booking that overlaps.
            if (roomId != -1)
            {
                bool overlaps = bookings
                    .Where(b => b.IsActive && b.RoomId == roomId)
                    .Any(b => !(end < b.StartDate || start > b.EndDate));

                Assert.False(overlaps);
            }
        }


        // ============================================================
        // CreateBooking tests
        // Moq
        // ============================================================

        [Fact]
        public async Task CreateBooking_WhenRoomAvailable_SetsRoomIdAndIsActive_AndCallsAdd_ReturnsTrue()
        {
            // Arrange
            var bookingRepoMock = new Mock<IRepository<Booking>>();
            var roomRepoMock = new Mock<IRepository<Room>>();

            roomRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[]
            {
                new Room { Id = 1 },
                new Room { Id = 2 }
            });

            // Room 1 is booked in [Today+3..Today+5], room 2 is free
            bookingRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[]
            {
                new Booking { RoomId = 1, IsActive = true, StartDate = DateTime.Today.AddDays(3), EndDate = DateTime.Today.AddDays(5) }
            });

            bookingRepoMock.Setup(r => r.AddAsync(It.IsAny<Booking>())).Returns(Task.CompletedTask);

            var sut = new BookingManager(bookingRepoMock.Object, roomRepoMock.Object);

            var booking = new Booking
            {
                StartDate = DateTime.Today.AddDays(4),
                EndDate = DateTime.Today.AddDays(4),
                IsActive = false,
                RoomId = 0
            };

            // Act
            bool created = await sut.CreateBooking(booking);

            // Assert 
            Assert.True(created);
            Assert.True(booking.IsActive);
            Assert.Equal(2, booking.RoomId);

            // Assert (interaction)
            bookingRepoMock.Verify(r => r.AddAsync(It.Is<Booking>(b =>
                b == booking && b.IsActive && b.RoomId == 2)), Times.Once);
        }

        [Fact]
        public async Task CreateBooking_WhenNoRoomAvailable_DoesNotCallAdd_ReturnsFalse()
        {
            // Arrange
            var bookingRepoMock = new Mock<IRepository<Booking>>();
            var roomRepoMock = new Mock<IRepository<Room>>();

            roomRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[]
            {
                new Room { Id = 1 },
                new Room { Id = 2 }
            });

            // Both rooms are booked
            bookingRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[]
            {
                new Booking { RoomId = 1, IsActive = true, StartDate = DateTime.Today.AddDays(1), EndDate = DateTime.Today.AddDays(10) },
                new Booking { RoomId = 2, IsActive = true, StartDate = DateTime.Today.AddDays(1), EndDate = DateTime.Today.AddDays(10) }
            });

            var sut = new BookingManager(bookingRepoMock.Object, roomRepoMock.Object);

            var booking = new Booking
            {
                StartDate = DateTime.Today.AddDays(4),
                EndDate = DateTime.Today.AddDays(5)
            };

            // Act
            bool created = await sut.CreateBooking(booking);

            // Assert
            Assert.False(created);
            bookingRepoMock.Verify(r => r.AddAsync(It.IsAny<Booking>()), Times.Never);
        }

        // ============================================================
        // GetFullyOccupiedDates tests
        // ============================================================

        [Fact]
        public async Task GetFullyOccupiedDates_StartAfterEnd_ThrowsArgumentException()
        {
            // Arrange
            DateTime start = DateTime.Today.AddDays(5);
            DateTime end = DateTime.Today.AddDays(4);

            // Act
            Task result() => bookingManager.GetFullyOccupiedDates(start, end);

            // Assert
            await Assert.ThrowsAsync<ArgumentException>(result);
        }

        [Fact]
        public async Task GetFullyOccupiedDates_NotFullyBooked_ReturnsEmpty()
        {
            // Arrange
            // FakeRoomRepository has 2 rooms.
            // FakeBookingRepository has only 1 booking on Today+1 for room 1.
            DateTime date = DateTime.Today.AddDays(1);

            // Act
            var result = await bookingManager.GetFullyOccupiedDates(date, date);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFullyOccupiedDates_FullyBookedRange_ReturnsAllDatesInRange()
        {
            // Arrange
            // FakeBookingRepository is fully occupied for both rooms in [Today+10..Today+20]
            DateTime start = DateTime.Today.AddDays(10);
            DateTime end = DateTime.Today.AddDays(20);

            // Act
            var result = await bookingManager.GetFullyOccupiedDates(start, end);

            // Assert: all dates in the interval should be returned
            int expectedCount = (end.Date - start.Date).Days + 1;
            Assert.Equal(expectedCount, result.Count);
            Assert.Equal(start.Date, result.First().Date);
            Assert.Equal(end.Date, result.Last().Date);
        }

        //Data-driven tests

        [Theory]
        [InlineData(-1, 5)]  // start in past
        [InlineData(5, 3)]   // start after end
        [InlineData(-2, -1)] // both dates in past
        public async Task FindAvailableRoom_InvalidDates_ThrowsArgumentException(int startOffset,int endOffset)
        {
            // Arrange
            var bookingRepoMock = new Mock<IRepository<Booking>>();
            var roomRepoMock = new Mock<IRepository<Room>>();

            var manager = new BookingManager(
                bookingRepoMock.Object,
                roomRepoMock.Object);

            var startDate = DateTime.Today.AddDays(startOffset);
            var endDate = DateTime.Today.AddDays(endOffset);

            // Act
            Func<Task> act = () => manager.FindAvailableRoom(startDate, endDate);

            // Assert
            await Assert.ThrowsAsync<ArgumentException>(act);
        }
    }
    }
}
