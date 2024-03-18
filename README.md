# XeSoft.EventStore
An event store library on top of PostgreSQL with a data-oriented API.

## Quickstart

_TODO_


## Q&A

### Why PostgreSQL?
Postgres is particularly suited to hosting a well-rounded event store due to its LISTEN/NOTIFY feature.


### Why use this over eventstore.com or Marten or NEventStore?
Definitely check out those projects and evaluate what fits your needs best.

This library was born out of the needs of a small team and budget. The goals were:
* minimize operational knowledge - use Postgres for all types of storage
* minimize operational cost - runs on t3 micro instances in production
* architectural options - external listeners and multiple simultaneous writers
* data-oriented API rather than an object-oriented one


### What are some notable limits?
Anecdotally, XeSoft.EventStore is capable of writing on the order of 100s to 1000s of events per second on an AWS RDS t3.micro instance. The primary reason for this limit is locking on the position table when new events are appended. However, this approach provides support for multiple simultaneous writers.

There is a limit on the number of event store connections, depending on server resources -- about 85 on a t3 micro instance. This includes pooled connections used for reading and writing events as well as each listener connection. This is an inherited limitation of Postgres, because each connection consumes server resources. We tyically set our API connection pool size to around 20, and use a dedicated connection for each listener.


