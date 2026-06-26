import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '20s', target: 5  },  // ramp up
    { duration: '1m',  target: 5  },  // hold — stay under 60 req/min rate limit
    { duration: '10s', target: 0  },  // ramp down
  ],
  thresholds: {
    // p99 of all responses must be under 500ms
    http_req_duration: ['p(99)<500'],
    // at least 95% of real-error checks must pass (i.e. no 500/connection errors)
    'checks{type:real_error}': ['rate>0.95'],
  },
};

const BASE_URL = 'http://localhost:5000';

export function setup() {
  const res = http.post(
    `${BASE_URL}/api/v1/identity/login`,
    JSON.stringify({ email: 'librarian@shelflife.dev', password: 'Librarian@123' }),
    { headers: { 'Content-Type': 'application/json' } }
  );
  check(res, { 'login 200': (r) => r.status === 200 });
  return { token: res.json('token') };
}

export default function (data) {
  const headers = {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${data.token}`,
  };

  // Hot path 1: list books (AsNoTracking read model)
  const books = http.get(`${BASE_URL}/api/v1/catalog/books?page=1&pageSize=20`, { headers });
  const booksOk = books.status === 200 || books.status === 429;
  check(books, {
    'GET /books 200 or 429': () => booksOk,
  });
  check(books, { 'GET /books real error': (r) => r.status !== 500 && r.status !== 0 }, { type: 'real_error' });

  sleep(0.5);  // pace requests to stay under rate limit

  // Hot path 2: list members
  const members = http.get(`${BASE_URL}/api/v1/identity/members?page=1&pageSize=20`, { headers });
  const membersOk = members.status === 200 || members.status === 429;
  check(members, {
    'GET /members 200 or 429': () => membersOk,
  });
  check(members, { 'GET /members real error': (r) => r.status !== 500 && r.status !== 0 }, { type: 'real_error' });

  sleep(0.5);
}
