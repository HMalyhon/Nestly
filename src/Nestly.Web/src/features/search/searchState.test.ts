import { describe, expect, it } from 'vitest';
import {
  DEFAULT_SEARCH, DEFAULT_VIEW, MAX_PAGE, MAX_QUERY_LENGTH, MAX_ZOOM, MIN_ZOOM, PAGE_SIZE,
  countFilters, pageCount, readSearchState, writeBounds, writeSearchState,
} from './searchState';
import type { SearchState } from './searchState';

const read = (query: string): SearchState => readSearchState(new URLSearchParams(query));

describe('readSearchState: bbox', () => {
  it('reads a viewport in Leaflet order', () => {
    // Act
    const { within } = read('bbox=-74.05,40.62,-73.85,40.82').filters;

    // Assert
    expect(within).toEqual({ leftLon: -74.05, bottomLat: 40.62, rightLon: -73.85, topLat: 40.82 });
  });

  // Each of these used to reach the API; the zero-area ones returned nothing, forever.
  it.each([
    ['a zero-area box at the origin', '0,0,0,0'],
    ['empty parts, which Number reads as zero', ',,,'],
    ['a box with no height', '-74,40.7,-73,40.7'],
    ['a box with no width', '-74,40,-74,41'],
    ['north below south', '-74,41,-73,40'],
    ['a latitude past the pole', '-74,40,-73,91'],
    ['a longitude past the antimeridian', '-181,40,-73,41'],
    ['too few parts', '-74,40,-73'],
    ['too many parts', '-74,40,-73,41,5'],
    ['a part that is not a number', '-74,40,east,41'],
    ['an infinite part', '-74,40,Infinity,41'],
  ])('ignores %s', (_, bbox) => {
    // Act
    const { within } = read(`bbox=${bbox}`).filters;

    // Assert
    expect(within).toBeUndefined();
  });

  it('round-trips through writeBounds', () => {
    // Arrange
    const within = { leftLon: -74.05197, bottomLat: 40.62125, rightLon: -73.85834, topLat: 40.8294 };

    // Act
    const roundTripped = read(`bbox=${writeBounds(within)}`).filters.within;

    // Assert
    expect(roundTripped).toEqual(within);
  });
});

describe('readSearchState: numbers', () => {
  // Number() accepts all of these, and each one reached the API as something nobody typed.
  it.each([
    ['hex', '0x10'],
    ['an exponent', '1e3'],
    ['a fraction', '1000.5'],
    ['a sign', '-500'],
    ['surrounding whitespace', ' 500 '],
    ['an empty value', ''],
  ])('drops a rent written as %s', (_, value) => {
    // Act
    const { minRent } = read(`minRent=${encodeURIComponent(value)}`).filters;

    // Assert
    expect(minRent).toBeUndefined();
  });

  it('keeps a rent written as plain digits', () => {
    // Act
    const { filters } = read('minRent=1500&maxRent=3000');

    // Assert
    expect(filters).toMatchObject({ minRent: 1500, maxRent: 3000 });
  });

  it('drops a rent too wide for the int the API binds it to, rather than clamping it', () => {
    // Act
    const { maxRent } = read('maxRent=2147483648').filters;

    // Assert
    expect(maxRent).toBeUndefined();
  });

  it('keeps neither bound of an inverted rent range', () => {
    // Act
    const { filters } = read('minRent=3000&maxRent=1000');

    // Assert
    expect(filters.minRent).toBeUndefined();
    expect(filters.maxRent).toBeUndefined();
  });

  it('reads beds=0 as a studio', () => {
    // Act
    const { bedrooms } = read('beds=0').filters;

    // Assert
    expect(bedrooms).toEqual([0]);
  });

  it('drops an empty beds value instead of turning it into a studio filter', () => {
    // Act
    const { bedrooms } = read('beds=').filters;

    // Assert
    expect(bedrooms).toBeUndefined();
  });

  it('drops a bedroom count past the byte the API binds it to', () => {
    // Act
    const { bedrooms } = read('beds=2&beds=256').filters;

    // Assert
    expect(bedrooms).toEqual([2]);
  });
});

describe('readSearchState: page and zoom', () => {
  it.each([
    ['missing', '', 1],
    ['zero', 'page=0', 1],
    ['past the last page the API serves', 'page=250', MAX_PAGE],
    ['not a number', 'page=two', 1],
  ])('reads a page that is %s as %i', (_, query, expected) => {
    // Act
    const { page } = read(query);

    // Assert
    expect(page).toBe(expected);
  });

  it('defaults the zoom only when there is none', () => {
    // Act
    const { zoom } = read('');

    // Assert
    expect(zoom).toBe(DEFAULT_VIEW.zoom);
  });

  it('clamps zoom 0 to the minimum rather than treating it as missing', () => {
    // Act
    const { zoom } = read('z=0');

    // Assert
    expect(zoom).toBe(MIN_ZOOM);
  });

  it('clamps a zoom past the tile range', () => {
    // Act
    const { zoom } = read('z=99');

    // Assert
    expect(zoom).toBe(MAX_ZOOM);
  });
});

describe('readSearchState: text and lists', () => {
  it('truncates the query at the length the API accepts', () => {
    // Arrange
    const long = 'a'.repeat(MAX_QUERY_LENGTH + 50);

    // Act
    const { query } = read(`q=${long}`);

    // Assert
    expect(query).toHaveLength(MAX_QUERY_LENGTH);
  });

  it('reads repeated parameters as a list and skips empty ones', () => {
    // Act
    const { boroughs } = read('borough=Brooklyn&borough=&borough=Queens').filters;

    // Assert
    expect(boroughs).toEqual(['Brooklyn', 'Queens']);
  });

  it('keeps names containing commas intact', () => {
    // Arrange
    const name = 'Bedford, Stuyvesant';

    // Act
    const { neighborhoods } = read(`hood=${encodeURIComponent(name)}`).filters;

    // Assert
    expect(neighborhoods).toEqual([name]);
  });

  it('caps amenities at twenty and other lists at fifty', () => {
    // Arrange
    const many = (param: string, count: number): string =>
      Array.from({ length: count }, (_, index) => `${param}=v${String(index)}`).join('&');

    // Act
    const { amenities } = read(many('amenity', 30)).filters;
    const { roomTypes } = read(many('room', 80)).filters;

    // Assert
    expect(amenities).toHaveLength(20);
    expect(roomTypes).toHaveLength(50);
  });

  it('falls back to relevance for a sort it does not offer', () => {
    // Act
    const { sort } = read('sort=DistanceAsc');

    // Assert
    expect(sort).toBe('Relevance');
  });
});

describe('writeSearchState', () => {
  it('writes nothing for the default search, so a plain link stays plain', () => {
    // Act
    const params = writeSearchState(DEFAULT_SEARCH);

    // Assert
    expect(params.toString()).toBe('');
  });

  it('keeps parameters it does not own', () => {
    // Arrange
    const current = new URLSearchParams('utm_source=hn&q=old&ref=newsletter');

    // Act
    const params = writeSearchState({ ...DEFAULT_SEARCH, query: 'loft' }, current);

    // Assert
    expect(params.get('utm_source')).toBe('hn');
    expect(params.get('ref')).toBe('newsletter');
    expect(params.get('q')).toBe('loft');
  });

  it('replaces the parameters it owns rather than appending to them', () => {
    // Arrange
    const current = new URLSearchParams('borough=Bronx&borough=Queens&page=4');

    // Act
    const params = writeSearchState({ ...DEFAULT_SEARCH, filters: { boroughs: ['Brooklyn'] } }, current);

    // Assert
    expect(params.getAll('borough')).toEqual(['Brooklyn']);
    expect(params.has('page')).toBe(false);
  });

  it('round-trips a full search through the URL', () => {
    // Arrange
    const state: SearchState = {
      query: 'sunny loft',
      sort: 'PriceAsc',
      page: 3,
      zoom: 14,
      filters: {
        boroughs: ['Brooklyn', 'Queens'],
        neighborhoods: ['Bedford, Stuyvesant'],
        amenities: ['Wifi'],
        bedrooms: [0, 2],
        minRent: 1000,
        maxRent: 4000,
        within: { leftLon: -74, bottomLat: 40.6, rightLon: -73.9, topLat: 40.8 },
      },
    };

    // Act
    const roundTripped = read(writeSearchState(state).toString());

    // Assert
    expect(roundTripped).toEqual(state);
  });
});

describe('countFilters', () => {
  it('counts every chosen value and each rent bound', () => {
    // Act
    const count = countFilters({ boroughs: ['Brooklyn', 'Queens'], bedrooms: [1], minRent: 500 });

    // Assert
    expect(count).toBe(4);
  });

  it('leaves the viewport out, since it is where the map is looking rather than a choice', () => {
    // Act
    const count = countFilters({ within: { leftLon: -74, bottomLat: 40, rightLon: -73, topLat: 41 } });

    // Assert
    expect(count).toBe(0);
  });
});

describe('pageCount', () => {
  it('rounds up a partial last page', () => {
    // Act
    const pages = pageCount(PAGE_SIZE + 1);

    // Assert
    expect(pages).toBe(2);
  });

  it('stops at the last page the API will serve', () => {
    // Act
    const pages = pageCount(PAGE_SIZE * (MAX_PAGE + 40));

    // Assert
    expect(pages).toBe(MAX_PAGE);
  });
});
