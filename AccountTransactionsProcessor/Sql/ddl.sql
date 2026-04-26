SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;
SET default_tablespace = '';
SET default_table_access_method = heap;

DROP TABLE IF EXISTS customer;
DROP TABLE IF EXISTS transactions;

CREATE TABLE customer (
  id SERIAL PRIMARY KEY,
  limit INTEGER NOT NULL,
  balance INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE transactions(
    id SERIAL PRIMARY KEY,
    amount  INTEGER NOT NULL,
    description VARCHAR(10) NOT NULL,
    type CHAR NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    customer_id INTEGER NOT NULL
);


CREATE INDEX idx_customer_id ON customer(id);

CREATE INDEX idx_transaction_customer_id ON transactions(customer_id);

INSERT INTO customer (limit, balance)
VALUES
    (1000 * 100, 0),
    (800 * 100, 0),
    (10000 * 100, 0),
    (100000 * 100, 0),
    (5000 * 100, 0);




