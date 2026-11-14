import { setupServer } from 'msw/node'

/** Shared MSW server: tests register per-test handlers with server.use(...). */
export const server = setupServer()
