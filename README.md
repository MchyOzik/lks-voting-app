# Example Voting App

A simple distributed application running across multiple Docker containers.

## Getting started

Download [Docker Desktop](https://www.docker.com/products/docker-desktop) for Mac or Windows. [Docker Compose](https://docs.docker.com/compose) will be automatically installed. On Linux, make sure you have the latest version of [Compose](https://docs.docker.com/compose/install/).

This solution uses Python, Node.js, .NET, with Redis for messaging and Postgres for storage.

Run in this directory to build and run the app:

```shell
docker compose up
```

The `vote` app will be running at [http://localhost:5000](http://localhost:5000), and the `results` will be at [http://localhost:5001](http://localhost:5001).


## Run the app in Kubernetes

The folder k8s-specifications contains the YAML specifications of the Voting App's services.

Run the following command to create the deployments and services. Note it will create these resources in your current namespace (`default` if you haven't changed it.)

```shell
kubectl create -f k8s-specifications/
```

The `vote` web app is then available on port 31000 on each host of the cluster, the `result` web app is available on port 31001.

To remove them, run:

```shell
kubectl delete -f k8s-specifications/
```

## Architecture

![Architecture diagram](architecture.excalidraw.png)

* A front-end web app in [Python](/vote) which lets you vote between two options
* A [Redis](https://hub.docker.com/_/redis/) which collects new votes
* A [.NET](/worker/) worker which consumes votes and stores them in…
* A [Postgres](https://hub.docker.com/_/postgres/) database backed by a Docker volume
* A [Node.js](/result) web app which shows the results of the voting in real time

## Environment Variables

You can configure external PostgreSQL and Redis connections using environment variables. When deploying, you can inject these directly or reference them from a secure store (e.g., AWS SSM Parameter Store).

*   `REDIS_HOST`: The hostname of your Redis server. Used by the `vote` and `worker` services.
    *   **Example:** `lks-redis.lfc5mu.ng.0001.use1.cache.amazonaws.com`
*   `POSTGRES_CONNECTION_STRING`: The connection string for your PostgreSQL database. Used by the `result` and `worker` services. Note that the expected format differs slightly depending on the service:
    *   **Example for `result` (Node.js):** `postgres://postgres:LKSNCC2024@lks-rds.cvnb1e2wtrmb.us-east-1.rds.amazonaws.com/postgres`
    *   **Example for `worker` (.NET):** `Server=lks-rds.cvnb1e2wtrmb.us-east-1.rds.amazonaws.com:5432;Username=postgres;Password=LKSNCC2024;Database=voting_data;SslMode=Prefer;Trust Server Certificate=true;`

## Notes

The voting application only accepts one vote per client browser. It does not register additional votes if a vote has already been submitted from a client.

This isn't an example of a properly architected perfectly designed distributed app... it's just a simple
example of the various types of pieces and languages you might see (queues, persistent data, etc), and how to
deal with them in Docker at a basic level.
