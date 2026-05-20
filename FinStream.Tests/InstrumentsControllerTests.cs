using Microsoft.AspNetCore.Mvc;
using Moq;
using FinStream.API.Controllers;
using FinStream.Application.DTOs;
using FinStream.Domain.Entities;
using FinStream.Domain.Interfaces;

namespace FinStream.Tests;

public class InstrumentsControllerTests
{
    private readonly Mock<IInstrumentRepository> _mockRepo;
    private readonly InstrumentsController _controller;

    public InstrumentsControllerTests()
    {
        _mockRepo = new Mock<IInstrumentRepository>();
        _controller = new InstrumentsController(_mockRepo.Object);
    }

    [Fact]
    public async Task GetAll_ShouldReturnAllInstruments()
    {
        var instruments = new List<Instrument>
        {
            new() { Id = Guid.NewGuid(), Symbol = "AAPL", Name = "Apple" },
            new() { Id = Guid.NewGuid(), Symbol = "GOOG", Name = "Google" }
        };
        _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(instruments);

        var result = await _controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<InstrumentDto>>(okResult.Value);
        Assert.Equal(2, dtos.Count());
    }

    [Fact]
    public async Task GetBySymbol_ShouldReturnInstrument_WhenExists()
    {
        var instrument = new Instrument { Id = Guid.NewGuid(), Symbol = "AAPL", Name = "Apple" };
        _mockRepo.Setup(r => r.GetBySymbolAsync("AAPL")).ReturnsAsync(instrument);

        var result = await _controller.GetBySymbol("AAPL");

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<InstrumentDto>(okResult.Value);
        Assert.Equal("AAPL", dto.Symbol);
    }

    [Fact]
    public async Task GetBySymbol_ShouldReturnNotFound_WhenNotExists()
    {
        _mockRepo.Setup(r => r.GetBySymbolAsync("INVALID")).ReturnsAsync((Instrument?)null);

        var result = await _controller.GetBySymbol("UNKNOWN");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_ShouldReturnCreated_WhenNewInstrument()
    {
        var dto = new CreateInstrumentDto("MSFT", "Microsoft");
        _mockRepo.Setup(r => r.GetBySymbolAsync("MSFT")).ReturnsAsync((Instrument?)null);
        _mockRepo.Setup(r => r.AddAsync(It.IsAny<Instrument>()))
            .ReturnsAsync((Instrument i) => i);

        var result = await _controller.Create(dto);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var returnedDto = Assert.IsType<InstrumentDto>(createdResult.Value);
        Assert.Equal("MSFT", returnedDto.Symbol);
    }

    [Fact]
    public async Task Create_ShouldReturnConflict_WhenInstrumentExists()
    {
        var existing = new Instrument { Symbol = "AAPL" };
        _mockRepo.Setup(r => r.GetBySymbolAsync("AAPL")).ReturnsAsync(existing);

        var dto = new CreateInstrumentDto("AAPL", "Apple");

        var result = await _controller.Create(dto);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Delete_ShouldReturnNoContent_WhenDeleted()
    {
        var instrument = new Instrument { Id = Guid.NewGuid(), Symbol = "AAPL" };
        _mockRepo.Setup(r => r.GetBySymbolAsync("AAPL")).ReturnsAsync(instrument);

        var result = await _controller.Delete("AAPL");

        Assert.IsType<NoContentResult>(result);
        _mockRepo.Verify(r => r.DeleteAsync(instrument.Id), Times.Once);
    }

    [Fact]
    public async Task Delete_ShouldReturnNotFound_WhenNotExists()
    {
        _mockRepo.Setup(r => r.GetBySymbolAsync("INVALID")).ReturnsAsync((Instrument?)null);

        var result = await _controller.Delete("INVALID");

        Assert.IsType<NotFoundResult>(result);
    }
}