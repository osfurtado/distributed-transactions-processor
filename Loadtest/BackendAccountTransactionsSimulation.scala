import scala.concurrent.duration._
import scala.util.Random

import io.gatling.core.Predef._
import io.gatling.http.Predef._

class AccountTransactionsSimulation extends Simulation {

  // =========================
  // Helpers
  // =========================

  def randomCustomerId() = Random.between(1, 6)
  def randomAmount() = Random.between(1, 10001)
  def randomDescription() = Random.alphanumeric.take(10).mkString

  val httpProtocol = http
    .baseUrl("http://localhost:9999")
    .contentTypeHeader("application/json")
    .acceptHeader("application/json")

  // =========================
  // Débitos
  // =========================

  val debits = scenario("debits")
    .exec { session =>
      session
        .set("customer_id", randomCustomerId())
        .set("amount", randomAmount())
        .set("description", randomDescription())
    }
    .exec(
      http("POST debit")
        .post("/customers/#{customer_id}/transactions")
        .body(StringBody(
          """{"amount": #{amount}, "type": "d", "description": "#{description}"}"""
        ))
        .check(
          status.in(200, 422),
          status.saveAs("status")
        )
        .checkIf(session => session("status").as[Int] == 200) {
          jsonPath("$.limit").ofType[Int].exists
        }
        .checkIf(session => session("status").as[Int] == 200) {
          jsonPath("$.balance").ofType[Int].exists
        }
    )

  // =========================
  // Créditos
  // =========================

  val credits = scenario("credits")
    .exec { session =>
      session
        .set("customer_id", randomCustomerId())
        .set("amount", randomAmount())
        .set("description", randomDescription())
    }
    .exec(
      http("POST credit")
        .post("/customers/#{customer_id}/transactions")
        .body(StringBody(
          """{"amount": #{amount}, "type": "c", "description": "#{description}"}"""
        ))
        .check(
          status.is(200),
          jsonPath("$.limit").ofType[Int].exists,
          jsonPath("$.balance").ofType[Int].exists
        )
    )

  // =========================
  // Extrato
  // =========================

  val statements = scenario("statements")
    .exec { session =>
      session.set("customer_id", randomCustomerId())
    }
    .exec(
      http("GET statement")
        .get("/customers/#{customer_id}/statement")
        .check(
          status.is(200),
          jsonPath("$.balance.limit").ofType[Int].exists,
          jsonPath("$.balance.total").ofType[Int].exists,
          jsonPath("$.last_transactions").exists
        )
    )

  // =========================
  // Validação concorrência
  // =========================

  val concurrentTransactions = (t: String) =>
    scenario(s"concurrent-$t")
      .exec(
        http("POST concurrent tx")
          .post("/customers/1/transactions")
          .body(StringBody(
            s"""{"amount": 1, "type": "$t", "description": "load"}"""
          ))
          .check(status.is(200))
      )

  val checkBalance = (expected: Int) =>
    scenario(s"check-balance-$expected")
      .exec(
        http("GET check balance")
          .get("/customers/1/statement")
          .check(
            status.is(200),
            jsonPath("$.balance.total").ofType[Int].is(expected)
          )
      )

  // =========================
  // Smoke / validação inicial
  // =========================

  val smoke = scenario("smoke")
    .foreach(1 to 5, "id") {
      exec(
        http("GET initial state")
          .get("/customers/#{id}/statement")
          .check(
            status.is(200),
            jsonPath("$.balance.total").ofType[Int].is(0)
          )
      )
    }

  val notFound = scenario("404-check")
    .exec(
      http("GET invalid customer")
        .get("/customers/999/statement")
        .check(status.is(404))
    )

  // =========================
  // Setup
  // =========================

  setUp(
    // 🔥 Validação concorrência inicial
    concurrentTransactions("d").inject(
      atOnceUsers(25)
    ).andThen(
      checkBalance(-25).inject(atOnceUsers(1))
    ).andThen(
      concurrentTransactions("c").inject(
        atOnceUsers(25)
      ).andThen(
        checkBalance(0).inject(atOnceUsers(1))
      )
    ).andThen(

      // 🔍 Smoke + sanity
      smoke.inject(atOnceUsers(1)),
      notFound.inject(atOnceUsers(1))

    ).andThen(

      // 🚀 Carga principal
      debits.inject(
        rampUsersPerSec(1).to(220).during(2.minutes),
        constantUsersPerSec(220).during(2.minutes)
      ),
      credits.inject(
        rampUsersPerSec(1).to(110).during(2.minutes),
        constantUsersPerSec(110).during(2.minutes)
      ),
      statements.inject(
        rampUsersPerSec(1).to(10).during(2.minutes),
        constantUsersPerSec(10).during(2.minutes)
      )
    )
  ).protocols(httpProtocol)
}