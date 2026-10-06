// A tiny SQL interface so the same code runs on Cloudflare D1 and on SQLite in the tests.
// Every query uses bound parameters; nothing is ever concatenated into SQL.

export type SqlValue = string | number | null;

export interface Db {
  first<T>(sql: string, ...params: SqlValue[]): Promise<T | null>;
  all<T>(sql: string, ...params: SqlValue[]): Promise<T[]>;
  run(sql: string, ...params: SqlValue[]): Promise<number>; // rows changed
}

/** Cloudflare D1. */
export function d1(db: D1Database): Db {
  return {
    async first<T>(sql: string, ...params: SqlValue[]) {
      return (await db.prepare(sql).bind(...params).first<T>()) ?? null;
    },
    async all<T>(sql: string, ...params: SqlValue[]) {
      const r = await db.prepare(sql).bind(...params).all<T>();
      return r.results ?? [];
    },
    async run(sql: string, ...params: SqlValue[]) {
      const r = await db.prepare(sql).bind(...params).run();
      return r.meta?.changes ?? 0;
    },
  };
}
