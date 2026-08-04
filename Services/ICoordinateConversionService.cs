using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public interface ICoordinateConversionService
{
    CoordinateConversionResult Convert(CoordinatePoint point, CoordinateSystemType source, CoordinateSystemType target);
}
