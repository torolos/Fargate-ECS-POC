describe('Basic Math', () => {
  test('adds numbers correctly', () => {
    expect(1 + 2).toBe(3);
  });

  test('multiplies numbers correctly', () => {
    expect(3 * 4).toBe(12);
  });
});

describe('Async Example', () => {
  const fetchData = () => Promise.resolve('demo-data');

  test('resolves with correct data', async () => {
    const data = await fetchData();
    expect(data).toBe('demo-data');
  });

  test('resolves using .resolves', () => {
    return expect(fetchData()).resolves.toBe('demo-data');
  });
});

describe('Mock Functions', () => {
  test('mock function gets called', () => {
    const mockFn = jest.fn();

    mockFn();
    mockFn();

    expect(mockFn).toHaveBeenCalled();
    expect(mockFn).toHaveBeenCalledTimes(2);
  });
});

describe('Object Comparison', () => {
  test('objects are equal', () => {
    const obj = { name: 'test', value: 123 };

    expect(obj).toEqual({
      name: 'test',
      value: 123,
    });
  });
});

// describe('Intentional Failure', () => {
//   test('this test fails (for pipeline demo)', () => {
//     expect(true).toBe(false);
//   });
// });